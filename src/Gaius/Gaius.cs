using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Gaius.Core.Configuration;
using Gaius.Processing.FileSystem;
using Gaius.Processing.Display;
using Gaius.Worker.MarkdownLiquid;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using Gaius.Core.Arguments;
using Gaius.ConsoleHostedService;
using Gaius.Server;
using Gaius.Server.BackgroundHostedService;
using Gaius.Worker.FrontMatter;
using Gaius.Worker.FrontMatter.Yaml;
using Gaius.Worker;

namespace Gaius
{
    internal sealed class Gaius
    {
        private static IConfigurationRoot BuildRootConfiguration(string basePath)
        {
            basePath = !string.IsNullOrEmpty(basePath) ? basePath : Directory.GetCurrentDirectory();

            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("gaius.json", optional: true, reloadOnChange: false)
                .Build();
        }

        private static IHostBuilder CreateHostBuilderForConsoleApp(GaiusArguments gaiusArgs)
        {
            return Host.CreateDefaultBuilder()
                    .UseContentRoot(gaiusArgs.BasePath)
                    .ConfigureAppConfiguration((hostBuilderContext, configBuilder) => {
                        configBuilder.AddJsonFile("gaius.json", optional : true, reloadOnChange : false);
                    })
                    .ConfigureServices((hostBuilderContext, serviceCollection) =>
                    {
                        serviceCollection
                        .Configure<GaiusConfiguration>(overrideConfig =>
                        {
                            overrideConfig.SiteContainerFullPath = gaiusArgs.BasePath;
                            overrideConfig.IsTestModeEnabled = gaiusArgs.IsTestModeEnabled;
                        })
                        .Configure<GaiusConfiguration>(hostBuilderContext.Configuration.GetSection("GaiusEngine"))
                        .AddHostedService<GaiusConsoleHostedService>()
                        .AddSingleton<ITerminalDisplayService, TerminalDisplayService>()
                        .AddSingleton<IFSProcessor, FSProcessor>()
                        .AddSingleton<IFrontMatterParser, YamlFrontMatterParser>()
                        .AddSingleton<IWorker, MarkdownLiquidWorker>()
                        .AddSingleton<GaiusArguments>(serviceProvider => gaiusArgs)
                        .AddOptions<GaiusConfiguration>()
                            .Bind(hostBuilderContext.Configuration.GetSection("GaiusEngine"));
                    })
                    .ConfigureLogging((hostBuilderContext, loggingBuilder) =>
                    {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.AddConsole();
                    });
        }

        private static IHostBuilder CreateHostBuilderForWebApp(GaiusArguments gaiusArgs)
        {
            var configurationRoot = BuildRootConfiguration(gaiusArgs.BasePath);

            var manualGaiusConfig = new GaiusConfiguration(gaiusArgs);
            configurationRoot.GetSection("GaiusEngine").Bind(manualGaiusConfig);
            var generationDirName = manualGaiusConfig.GenerationDirectoryName;

            return Host.CreateDefaultBuilder()
                    .UseContentRoot(gaiusArgs.BasePath)
                    .ConfigureAppConfiguration((hostBuilderContext, configBuilder) => {
                        configBuilder.AddJsonFile("gaius.json", optional : true, reloadOnChange : false);
                    })
                    .ConfigureServices((hostBuilderContext, serviceCollection) =>
                    {
                        serviceCollection
                        .Configure<GaiusConfiguration>(overrideConfig =>
                        {
                            overrideConfig.SiteContainerFullPath = gaiusArgs.BasePath;
                            overrideConfig.IsTestModeEnabled = gaiusArgs.IsTestModeEnabled;
                        })
                        .Configure<GaiusConfiguration>(hostBuilderContext.Configuration.GetSection("GaiusEngine"))
                        .AddHostedService<BuildRequestQueueProcessorHostedService>()
                        .AddSingleton<GaiusFileSystemWatcher>()
                        .AddSingleton<IBuildRequestQueue, BuildRequestQueue>()
                        .AddSingleton<IFSProcessor, FSProcessor>()
                        .AddSingleton<IFrontMatterParser, YamlFrontMatterParser>()
                        .AddSingleton<IWorker, MarkdownLiquidWorker>()
                        .AddSingleton<GaiusArguments>(serviceProvider => gaiusArgs)
                        .AddOptions<GaiusConfiguration>()
                            .Bind(hostBuilderContext.Configuration.GetSection("GaiusEngine"));
                    })
                    .ConfigureWebHostDefaults((webBuilder) =>
                    {
                        webBuilder.UseWebRoot(generationDirName);
                        webBuilder.UseStartup<Startup>();
                    })
                    .ConfigureLogging((hostBuilderContext, loggingBuilder) => {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.AddConsole();
                    });
        }

        private static async Task Main(string[] args)
        {
            var gaiusArgs = new GaiusArguments(args);

            var consoleHostBuilder = CreateHostBuilderForConsoleApp(gaiusArgs);
            await consoleHostBuilder.RunConsoleAsync();

            if(Environment.ExitCode == 0 && gaiusArgs.IsServeCommand)
            {
                var webHostBuilder = CreateHostBuilderForWebApp(gaiusArgs);
                var webHost = webHostBuilder.Build();
                var sourceDataFileSystemWatcher = webHost.Services.GetRequiredService<GaiusFileSystemWatcher>();
                sourceDataFileSystemWatcher.StartWatcher();
                await webHost.RunAsync();
            }
        }
    }    
}