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

namespace Gaius.ConsoleHostedService
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

            _appLifetime.ApplicationStarted.Register(OnStarted);
            _appLifetime.ApplicationStopped.Register(OnStopped);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Starting Gaius console hosted service");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Stopping Gaius console hosted service with exit code: {_exitCode}");

            // Exit code may be null if the user cancelled via Ctrl+C/SIGTERM
            Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
            return Task.CompletedTask;
        }

        private void OnStarted()
        {
            try
            {
                _exitCode = 0;

                if (!Directory.Exists(_gaiusConfigruation.SiteContainerFullPath))
                {
                    TerminalUtilities.PrintBasePathDoesNotExist(_gaiusConfigruation.SiteContainerFullPath);
                    _exitCode = 1;
                    return;
                }

                if (_worker == null || _fsProcessor == null)
                {
                    TerminalUtilities.PrintInvalidConfiguration();
                    _exitCode = 1;
                    return;
                }

                if (_gaiusArgs.IsUnknownCommand)
                {
                    TerminalUtilities.PrintUnknownCommand(_gaiusArgs.Command);
                    _exitCode = 1;
                    return;
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
                    _worker.InitWorker();

                    (var isContainerDirectoryValid, var validationErrors) = _worker.ValidateSiteContainerDirectory();

                    if (!isContainerDirectoryValid)
                    {
                        TerminalUtilities.PrintSiteContainerDirectoryNotValid(validationErrors, _gaiusConfigruation);
                        _exitCode = 1;
                        return;
                    }

                    var opTree = _fsProcessor.CreateFSOperationTree();
                    _terminalDisplayService.PrintOperationTree(opTree);

                    if (!_gaiusArgs.IsAutomaticYesEnabled && !TerminalUtilities.YesToContinue())
                        return;

                    _fsProcessor.ProcessFSOperationTree(opTree);
                    _terminalDisplayService.PrintOperationTree(opTree);
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
        }
    
        private void OnStopped() 
        {
            if(_gaiusArgs.IsServeCommand)
                _logger.LogDebug($"Stopped Gaius console hosted service, Gaius server will start momentarily");
            
            else
                _logger.LogDebug($"Stopped Gaius console hosted service");
        }
    }
}