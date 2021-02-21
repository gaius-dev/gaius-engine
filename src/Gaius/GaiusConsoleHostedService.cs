using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Gaius.Core.Arguments;
using Gaius.Core.Configuration;
using Gaius.Core.Terminal;
using Gaius.Processing.Display;
using Gaius.Processing.FileSystem;
using Gaius.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gaius
{
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

                        if (_exitCode == 0)
                        {
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

                                if (_exitCode == 0)
                                {
                                    var opTree = _fsProcessor.CreateFSOperationTree();
                                    _terminalDisplayService.PrintOperationTree(opTree);

                                    if (_gaiusArgs.IsAutomaticYesEnabled || TerminalUtilities.YesToContinue())
                                    {
                                        _fsProcessor.ProcessFSOperationTree(opTree);
                                        _terminalDisplayService.PrintOperationTree(opTree);
                                    }
                                }
                            }
                        }
                    }

                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception!");
                        _exitCode = 1;
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