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
                        .AddSingleton<ITerminalDisplayService, TerminalDisplayService>()
                        .AddSingleton<IFSProcessor, FSProcessor>()
                        .AddSingleton<GaiusArguments>(serviceProvider => gaiusArgs)
                        .AddOptions<GaiusConfiguration>()
                            .Bind(hostBuilderContext.Configuration.GetSection("GaiusEngine"));

                        var gaiusConfiguration = new GaiusConfiguration(gaiusArgs);
                        hostBuilderContext.Configuration.GetSection("GaiusEngine").Bind(gaiusConfiguration);

                        if (gaiusConfiguration.Worker.Equals("Gaius.Worker.MarkdownLiquid.MarkdownLiquidWorker"))
                            serviceCollection = MarkdownLiquidWorker.ConfigureServicesForWorker(serviceCollection);
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
                        .AddSingleton<GaiusArguments>(serviceProvider => gaiusArgs)
                        .AddOptions<GaiusConfiguration>()
                            .Bind(hostBuilderContext.Configuration.GetSection("GaiusEngine"));

                        var gaiusConfiguration = new GaiusConfiguration(gaiusArgs);
                        hostBuilderContext.Configuration.GetSection("GaiusEngine").Bind(gaiusConfiguration);

                        if (gaiusConfiguration.Worker.Equals("Gaius.Worker.MarkdownLiquid.MarkdownLiquidWorker"))
                            serviceCollection = MarkdownLiquidWorker.ConfigureServicesForWorker(serviceCollection);
                    })
                    .ConfigureLogging((hostBuilderContext, loggingBuilder) =>
                    {
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
                await webHostBuilder.Build().RunAsync();
            }
        }
    }    
}