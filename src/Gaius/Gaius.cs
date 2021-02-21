using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Gaius.Core.Configuration;
using Gaius.Processing.FileSystem;
using Gaius.Core.Terminal;
using Gaius.Processing.Display;
using Gaius.Worker;
using Gaius.Worker.MarkdownLiquid;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace Gaius
{
    public class Gaius
    {
        private static IConfigurationRoot BuildConfiguration(string basePath)
        {
            basePath = !string.IsNullOrEmpty(basePath) ? basePath : Directory.GetCurrentDirectory();

            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("gaius.json", optional : true, reloadOnChange : true)
                .Build();
        }

        private static (IServiceCollection, bool) ConfigureServices(IConfigurationRoot configRoot, string basePath, bool isTestCommand)
        {
            var serviceCollection = new ServiceCollection()
                .AddOptions()
                .Configure<GaiusConfiguration>(overrideConfig => 
                {
                    overrideConfig.SiteContainerFullPath = basePath;
                    overrideConfig.IsTestMode = isTestCommand;
                })
                .Configure<GaiusConfiguration>(configRoot.GetSection("GaiusEngine"))
                .AddSingleton<ITerminalDisplayService, TerminalDisplayService>()
                .AddSingleton<IFSProcessor, FSProcessor>();

            var gaiusConfiguration = new GaiusConfiguration();
            gaiusConfiguration.SiteContainerFullPath = basePath;
            gaiusConfiguration.IsTestMode = isTestCommand;
            configRoot.GetSection("GaiusEngine").Bind(gaiusConfiguration);

            var workerConfigured = false;
            if (gaiusConfiguration.Worker.Equals("Gaius.Worker.MarkdownLiquid.MarkdownLiquidWorker"))
            {
                serviceCollection = MarkdownLiquidWorker.ConfigureServicesForWorker(serviceCollection);
                workerConfigured = true;
            }

            return (serviceCollection, workerConfigured);
        }

        private static (IConfigurationRoot, IServiceCollection, bool) ConfigureConsoleApplication(string basePath, bool isTestCommand)
        {
            var config = BuildConfiguration(basePath);
            (var serviceCollection, var workerConfigured) = ConfigureServices(config, basePath, isTestCommand);
            return (config, serviceCollection, workerConfigured);
        }

        private static IHostBuilder CreateHostBuilder(string basePath, GaiusConfiguration gaiusConfiguration)
        {
            var hostBuilder = Host.CreateDefaultBuilder()
                .UseContentRoot(basePath)
                .ConfigureAppConfiguration((hostBuilderContext, configBuilder) => {
                    configBuilder.AddJsonFile("gaius.json", optional : true, reloadOnChange : true);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseWebRoot(gaiusConfiguration.GenerationDirectoryName);
                    webBuilder.UseStartup<Startup>();
                });

            return hostBuilder;
        }

        public static void Main(string[] args)
        {
            var gaiusArgs = new GaiusArgs(args);

            if(!Directory.Exists(gaiusArgs.BasePath))
            {
                TerminalUtilities.PrintBasePathDoesNotExist(gaiusArgs.BasePath);
                return;
            }

            (var configRoot, var serviceCollection, var validConfiguration) 
                = ConfigureConsoleApplication(gaiusArgs.BasePath, gaiusArgs.IsTestModeEnabled);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var terminalOutputService = serviceProvider.GetService<ITerminalDisplayService>();
            var gaiusConfiguration = serviceProvider.GetService<IOptions<GaiusConfiguration>>().Value;

            // commands that don't require full initialization and validation
            if(gaiusArgs.NoCommand)
            {
                TerminalUtilities.PrintDefault();
                return;
            }

            if(gaiusArgs.IsUnknownCommand)
            {
                TerminalUtilities.PrintUnknownCommand(gaiusArgs.Command);
                return;
            }

            if(gaiusArgs.IsVersionCommand)
            {
                TerminalUtilities.PrintVersionCommand();
                return;
            }

            if(gaiusArgs.IsHelpCommand)
            {
                TerminalUtilities.PrintHelpCommand();
                return;
            }

            // explicitly validate configuration for remaining commands
            if(!validConfiguration)
            {
                TerminalUtilities.PrintInvalidConfiguration();
                return;
            }

            if(gaiusArgs.IsShowConfigCommand)
            {
                TerminalUtilities.PrintShowConfigurationCommand(gaiusArgs.BasePath, gaiusConfiguration);
                return;
            }

            // additional initialization required for remaining commands
            var fileSystemProcessor = serviceProvider.GetService<IFSProcessor>();
            var worker = serviceProvider.GetService<IWorker>();

            if(gaiusArgs.IsBuildCommand || gaiusArgs.IsServeCommand)
            {
                (var isContainerDirValid, var validationErrors) = worker.ValidateSiteContainerDirectory();

                if(!isContainerDirValid)
                {
                    TerminalUtilities.PrintSiteContainerDirectoryNotValid(validationErrors, gaiusConfiguration);
                    return;
                }

                var opTree = fileSystemProcessor.CreateFSOperationTree();
                terminalOutputService.PrintOperationTree(opTree);

                if(!gaiusArgs.IsAutomaticYesEnabled && !TerminalUtilities.YesToContinue())
                    return;

                fileSystemProcessor.ProcessFSOperationTree(opTree);
                terminalOutputService.PrintOperationTree(opTree);

                if(!gaiusArgs.IsServeCommand)
                    return;

                var hostBuilder = CreateHostBuilder(gaiusArgs.BasePath, gaiusConfiguration);

                if(hostBuilder == null)
                    return;

                hostBuilder.Build().Run();
            }
        }
    }
}