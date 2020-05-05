using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Gaius.Core.Configuration;
using Gaius.Core.Processing.FileSystem;
using Gaius.Core.Terminal;
using Strube.Utilities.Terminal;
using Gaius.Core.Parsing;
using Gaius.Core.Parsing.Yaml;
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
                    overrideConfig.SiteContainerPath = basePath;
                })
                .Configure<GaiusConfiguration>(config)
                .AddSingleton<ITerminalOutputService, TerminalOutputService>();

                var gaiusConfiguration = new GaiusConfiguration();
                gaiusConfiguration.SiteContainerPath = basePath;
                config.Bind(gaiusConfiguration);

                bool processorConfigured = false;
                if(gaiusConfiguration.SupportedProcessors.Contains(gaiusConfiguration.Processor))
                {
                    serviceCollection
                        .AddSingleton<IFSProcessor, FSProcessor>();
                    processorConfigured = true;
                }

                bool pipelineConfigured = false;
                if(gaiusConfiguration.SupportedPipelines.Contains(gaiusConfiguration.Pipeline))
                {
                    serviceCollection
                        .AddSingleton<IFrontMatterParser, YamlFrontMatterParser>()
                        .AddSingleton<IWorker, MarkdownLiquidWorker>();
                    pipelineConfigured = true;
                }
                
            return (serviceCollection, processorConfigured && pipelineConfigured);
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

                    var validationResults = fileSystemProcessor.ValidateSiteContainerDir();

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