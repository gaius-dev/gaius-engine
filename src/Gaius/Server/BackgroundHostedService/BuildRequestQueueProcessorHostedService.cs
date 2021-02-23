using System;
using System.Threading;
using System.Threading.Tasks;
using Gaius.Core.Configuration;
using Gaius.Processing.FileSystem;
using Gaius.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gaius.Server.BackgroundHostedService
{
    public class BuildRequestQueueProcessorHostedService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly GaiusConfiguration _gaiusConfiguration;
        public IBuildRequestQueue _buildRequestQueue;
        private readonly IWorker _worker;
        private readonly IFSProcessor _fsProcessor;

        public BuildRequestQueueProcessorHostedService(ILogger<BuildRequestQueueProcessorHostedService> logger,
                                                       IOptions<GaiusConfiguration> gaiusConfigurationOptions,
                                                       IBuildRequestQueue buildRequestQueue,
                                                       IWorker worker,
                                                       IFSProcessor fsProcessor)
        {
            _logger = logger;
            _gaiusConfiguration = gaiusConfigurationOptions.Value;
            _buildRequestQueue = buildRequestQueue;
            _worker = worker;
            _fsProcessor = fsProcessor;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting build request queue processor, listening for build requests");

            await ListenForBuildRequests(stoppingToken);
        }

        private async Task ListenForBuildRequests(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var buildRequest = await _buildRequestQueue.DequeueBuildRequestAsync(stoppingToken);

                if(buildRequest == null || stoppingToken.IsCancellationRequested)
                    return;

                try
                {
                    _logger.LogInformation($"Rebuilding site using source data in {_gaiusConfiguration.SourceDirectoryFullPath}");

                    _worker.InitWorker();
                    var opTree = _fsProcessor.CreateFSOperationTree();
                    _fsProcessor.ProcessFSOperationTree(opTree);

                    _logger.LogInformation($"Rebuild complete");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, string.Empty);
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Stopping build request queue processor");

            await base.StopAsync(stoppingToken);
        }
    }
}