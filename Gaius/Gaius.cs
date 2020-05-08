using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Gaius.Core.Configuration;
using Gaius.Core.Processing.FileSystem;
using Gaius.Core.Terminal;
using Strube.Utilities.Terminal;
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

        public static (IServiceCollection, bool) ConfigureServices(IConfigurationRoot config, string basePath)
        {
            var serviceCollection = new ServiceCollection()
                .AddOptions()
                .Configure<GaiusConfiguration>(overrideConfig => 
                {
                    overrideConfig.SiteContainerFullPath = basePath;
                })
                .Configure<GaiusConfiguration>(config)
                .AddSingleton<ITerminalOutputService, TerminalOutputService>()
                .AddSingleton<IFSProcessor, FSProcessor>();

            var gaiusConfiguration = new GaiusConfiguration();
            gaiusConfiguration.SiteContainerFullPath = basePath;
            config.Bind(gaiusConfiguration);

            var workerConfigured = false;
            if (gaiusConfiguration.Worker.Equals("Gaius.Core.Worker.MarkdownLiquid.MarkdownLiquidWorker"))
            {
                serviceCollection = MarkdownLiquidWorker.ConfigureServicesForWorker(serviceCollection);
                workerConfigured = true;
            }

            return (serviceCollection, workerConfigured);
        }

        public static (IConfigurationRoot, IServiceCollection, bool) ConfigureConsoleApplication(string basePath)
        {
            var config = CreateConfiguration(basePath);
            var serviceCollection = ConfigureServices(config, basePath);
            return (config, serviceCollection.Item1, serviceCollection.Item2);
        }

        private const string AUTOMATIC_YES_PARAM = "-y";

        static void Main(string[] args)
        {
            var listArgs = new List<string>(args);

            var isAutomaticYesEnabled = listArgs.Contains(AUTOMATIC_YES_PARAM);

            if(isAutomaticYesEnabled)
                listArgs.Remove(AUTOMATIC_YES_PARAM);
                
            var pathArg = listArgs.Count <= 1 ? "." : listArgs[1];
            pathArg = Path.GetFullPath(pathArg);

            if(!Directory.Exists(pathArg))
            {
                TerminalUtilities.PrintBasePathDoesNotExist(pathArg);
                System.Console.WriteLine();
                return;
            }
            
            var appConfiguration = ConfigureConsoleApplication(pathArg);

            var config = appConfiguration.Item1;
            var serviceProvider = appConfiguration.Item2.BuildServiceProvider();
            var validConfiguration = appConfiguration.Item3;
            
            var terminalOutputService = serviceProvider.GetService<ITerminalOutputService>();

            if(!validConfiguration)
            {
                terminalOutputService.PrintInvalidConfiguration(pathArg);
                return;
            }

            if(listArgs.Count == 0)
            {
                terminalOutputService.PrintDefault();
                return;
            }

            var commandArg = listArgs[0].ToLowerInvariant();

            var fileSystemProcessor = serviceProvider.GetService<IFSProcessor>();
            var worker = serviceProvider.GetService<IWorker>();

            switch(commandArg)
            {
                case "version":
                    terminalOutputService.PrintVersionCommand();
                    return;

                case "help":
                    terminalOutputService.PrintHelpCommand();
                    return;

                case "showconfig":
                    terminalOutputService.PrintShowConfigurationCommand(pathArg);
                    return;

                case "process":

                    var validationResults = worker.ValidateSiteContainerDirectory();

                    if(!validationResults.Item1)
                    {
                        terminalOutputService.PrintSiteContainerDirectoryNotValid(validationResults.Item2);
                        return;
                    }

                    var opTree = fileSystemProcessor.CreateFSOperationTree();
                    terminalOutputService.PrintOperationTree(opTree);

                    if(!isAutomaticYesEnabled && !TerminalUtilities.YesToContinue())
                        return;

                    fileSystemProcessor.ProcessFSOperationTree(opTree);
                    terminalOutputService.PrintOperationTree(opTree);
                    return;
                    
                default:
                    terminalOutputService.PrintUnknownCommand(commandArg);
                    return;
            }
        }
    }
}