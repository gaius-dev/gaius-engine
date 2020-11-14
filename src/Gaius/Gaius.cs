using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Gaius.Core.Configuration;
using Gaius.Core.Processing.FileSystem;
using Gaius.Core.Terminal;
using Gaius.Utilities.Terminal;
using Gaius.Core.Worker;
using Gaius.Core.Worker.MarkdownLiquid;

namespace Gaius
{
    public abstract class Gaius
    {
        public static IConfigurationRoot CreateConfiguration(string basePath)
        {
            basePath = !string.IsNullOrEmpty(basePath) ? basePath : Directory.GetCurrentDirectory();

            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("gaius.json", optional : true, reloadOnChange : true)
                .AddEnvironmentVariables()
                .Build();
        }

        public static (IServiceCollection, bool) ConfigureServices(IConfigurationRoot config, string basePath, bool isTestCommand)
        {
            var serviceCollection = new ServiceCollection()
                .AddOptions()
                .Configure<GaiusConfiguration>(overrideConfig => 
                {
                    overrideConfig.SiteContainerFullPath = basePath;
                    overrideConfig.IsTestCommand = isTestCommand;
                })
                .Configure<GaiusConfiguration>(config)
                .AddSingleton<ITerminalOutputService, TerminalOutputService>()
                .AddSingleton<IFSProcessor, FSProcessor>();

            var gaiusConfiguration = new GaiusConfiguration();
            gaiusConfiguration.SiteContainerFullPath = basePath;
            gaiusConfiguration.IsTestCommand = isTestCommand;
            config.Bind(gaiusConfiguration);

            var workerConfigured = false;
            if (gaiusConfiguration.Worker.Equals("Gaius.Core.Worker.MarkdownLiquid.MarkdownLiquidWorker"))
            {
                serviceCollection = MarkdownLiquidWorker.ConfigureServicesForWorker(serviceCollection);
                workerConfigured = true;
            }

            return (serviceCollection, workerConfigured);
        }

        public static (IConfigurationRoot, IServiceCollection, bool) ConfigureConsoleApplication(string basePath, bool isTestCommand)
        {
            var config = CreateConfiguration(basePath);
            var serviceCollection = ConfigureServices(config, basePath, isTestCommand);
            return (config, serviceCollection.Item1, serviceCollection.Item2);
        }

        static void Main(string[] args)
        {
            var gaiusArgs = new GaiusArgs(args);

            if(!Directory.Exists(gaiusArgs.BasePath))
            {
                TerminalUtilities.PrintBasePathDoesNotExist(gaiusArgs.BasePath);
                return;
            }

            var appConfiguration = ConfigureConsoleApplication(gaiusArgs.BasePath, gaiusArgs.IsTestCommand);
            var config = appConfiguration.Item1;
            var serviceProvider = appConfiguration.Item2.BuildServiceProvider();
            var validConfiguration = appConfiguration.Item3;
            
            var terminalOutputService = serviceProvider.GetService<ITerminalOutputService>();

            // commands that don't require full initialization and validation
            if(gaiusArgs.IsNoCommand)
            {
                terminalOutputService.PrintDefault();
                return;
            }

            if(gaiusArgs.IsUnknownCommand)
            {
                terminalOutputService.PrintUnknownCommand(gaiusArgs.Command);
                return;
            }

            if(gaiusArgs.IsVersionCommand)
            {
                terminalOutputService.PrintVersionCommand();
                return;
            }

            if(gaiusArgs.IsHelpCommand)
            {
                terminalOutputService.PrintHelpCommand();
                return;
            }

            // explicitly validate configuration for remaining commands
            if(!validConfiguration)
            {
                terminalOutputService.PrintInvalidConfiguration(gaiusArgs.BasePath);
                return;
            }

            if(gaiusArgs.IsShowConfigCommand)
            {
                terminalOutputService.PrintShowConfigurationCommand(gaiusArgs.BasePath);
                return;
            }

            // additional initialization required for remaining commands
            var fileSystemProcessor = serviceProvider.GetService<IFSProcessor>();
            var worker = serviceProvider.GetService<IWorker>();

            if(gaiusArgs.IsTestCommand || gaiusArgs.IsProcessCommand)
            {
                var validationResults = worker.ValidateSiteContainerDirectory();

                if(!validationResults.Item1)
                {
                    terminalOutputService.PrintSiteContainerDirectoryNotValid(validationResults.Item2);
                    return;
                }

                var opTree = fileSystemProcessor.CreateFSOperationTree();
                terminalOutputService.PrintOperationTree(opTree);

                if(!gaiusArgs.IsAutomaticYesEnabled && !TerminalUtilities.YesToContinue())
                    return;

                fileSystemProcessor.ProcessFSOperationTree(opTree);
                terminalOutputService.PrintOperationTree(opTree);
                return;
            }
        }
    }
}