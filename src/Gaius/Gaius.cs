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
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using System.Threading;
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

        private static IHostBuilder CreateBaseHostBuilderForWebApp(GaiusArguments gaiusArgs)
        {
            var configurationRoot = BuildRootConfiguration(gaiusArgs.BasePath);

            var gaiusConfiguration = new GaiusConfiguration();
            gaiusConfiguration.SiteContainerFullPath = gaiusArgs.BasePath;
            gaiusConfiguration.IsTestMode = gaiusArgs.IsTestModeEnabled;
            configurationRoot.GetSection("GaiusEngine").Bind(gaiusConfiguration);

            return Host.CreateDefaultBuilder()
                    .UseContentRoot(gaiusArgs.BasePath)
                    .ConfigureAppConfiguration((hostBuilderContext, configBuilder) => {
                        configBuilder.AddJsonFile("gaius.json", optional : true, reloadOnChange : false);
                    })
                    .ConfigureWebHostDefaults((webBuilder) =>
                    {
                        webBuilder.UseWebRoot(gaiusConfiguration.GenerationDirectoryName);
                        webBuilder.UseStartup<Startup>();
                    })
                    .ConfigureLogging((hostBuilderContext, loggingBuilder) => {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.AddConsole();
                    });
        }

        private static IHostBuilder CreateBaseHostBuilderForConsoleApp(GaiusArguments gaiusArgs)
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
                            overrideConfig.IsTestMode = gaiusArgs.IsTestModeEnabled;
                        })
                        .Configure<GaiusConfiguration>(hostBuilderContext.Configuration.GetSection("GaiusEngine"))
                        .AddHostedService<GaiusConsoleHostedService>()
                        .AddSingleton<ITerminalDisplayService, TerminalDisplayService>()
                        .AddSingleton<IFSProcessor, FSProcessor>()
                        .AddTransient<GaiusArguments>(serviceProvider => gaiusArgs)
                        .AddOptions<GaiusConfiguration>()
                            .Bind(hostBuilderContext.Configuration.GetSection("GaiusEngine"));

                        var gaiusConfiguration = new GaiusConfiguration();
                        gaiusConfiguration.SiteContainerFullPath = gaiusArgs.BasePath;
                        gaiusConfiguration.IsTestMode = gaiusArgs.IsTestModeEnabled;
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

            var consoleHostBuilder = CreateBaseHostBuilderForConsoleApp(gaiusArgs);
            await consoleHostBuilder.RunConsoleAsync();

            if(gaiusArgs.IsServeCommand)
            {
                var webHostBuilder = CreateBaseHostBuilderForWebApp(gaiusArgs);
                await webHostBuilder.Build().RunAsync();
            }
        }
    }

    internal sealed class GaiusConsoleHostedService : IHostedService
    {
        private int? _exitCode;
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly GaiusArguments _gaiusArgs;
        private readonly GaiusConfiguration _gaiusConfigruation;
        private readonly ITerminalDisplayService _terminalDisplayService;
        private readonly IFSProcessor _fsProcessor;
        private readonly IWorker _worker;

        public GaiusConsoleHostedService(
            ILogger<GaiusConsoleHostedService> logger,
            IHostApplicationLifetime appLifetime,
            GaiusArguments gaiusArgs,
            IOptions<GaiusConfiguration> gaiusConfigurationOptions,
            ITerminalDisplayService terminalDisplayService,
            IFSProcessor fSProcessor,
            IWorker worker)
        {
            _logger = logger;
            _appLifetime = appLifetime;
            _gaiusArgs = gaiusArgs;
            _gaiusConfigruation = gaiusConfigurationOptions.Value;
            _terminalDisplayService = terminalDisplayService;
            _fsProcessor = fSProcessor;
            _worker = worker;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(() =>
                {
                    try
                    {
                        _exitCode = 0;

                        if (!Directory.Exists(_gaiusConfigruation.SiteContainerFullPath))
                        {
                            TerminalUtilities.PrintBasePathDoesNotExist(_gaiusConfigruation.SiteContainerFullPath);
                            _exitCode = 1;
                            _appLifetime.StopApplication();
                        }

                        if (_worker == null || _fsProcessor == null)
                        {
                            TerminalUtilities.PrintInvalidConfiguration();
                            _exitCode = 1;
                            _appLifetime.StopApplication();
                        }

                        if (_gaiusArgs.IsUnknownCommand)
                        {
                            TerminalUtilities.PrintUnknownCommand(_gaiusArgs.Command);
                            _exitCode = 1;
                            _appLifetime.StopApplication();
                        }

                        if (_gaiusArgs.NoCommand)
                            TerminalUtilities.PrintDefault();

                        else if (_gaiusArgs.IsHelpCommand)
                            TerminalUtilities.PrintHelpCommand();

                        else if (_gaiusArgs.IsVersionCommand)
                            TerminalUtilities.PrintVersionCommand();
                        
                        else if (_gaiusArgs.IsShowConfigCommand)
                            TerminalUtilities.PrintShowConfigurationCommand(_gaiusConfigruation);

                        else if (_gaiusArgs.IsBuildCommand || _gaiusArgs.IsServeCommand)
                        {
                            (var isContainerDirectoryValid, var validationErrors) = _worker.ValidateSiteContainerDirectory();

                            if (!isContainerDirectoryValid)
                            {
                                TerminalUtilities.PrintSiteContainerDirectoryNotValid(validationErrors, _gaiusConfigruation);
                                _exitCode = 1;
                                _appLifetime.StopApplication();
                            }

                            var opTree = _fsProcessor.CreateFSOperationTree();
                            _terminalDisplayService.PrintOperationTree(opTree);

                            if (!_gaiusArgs.IsAutomaticYesEnabled && !TerminalUtilities.YesToContinue())
                                _appLifetime.StopApplication();

                            _fsProcessor.ProcessFSOperationTree(opTree);
                            _terminalDisplayService.PrintOperationTree(opTree);
                        }
                    }

                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception!");
                    }

                    finally
                    {
                        _appLifetime.StopApplication();
                    }
                });
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Exiting with exit code: {_exitCode}");

            // Exit code may be null if the user cancelled via Ctrl+C/SIGTERM
            Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
            return Task.CompletedTask;
        }
    }
}