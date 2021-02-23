using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Gaius.Server.BackgroundHostedService
{
    public class BuildRequestQueue : IBuildRequestQueue
    {
        private ILogger _logger;
        private static BlockingCollection<BuildRequest> _buildRequests = new BlockingCollection<BuildRequest>(1);
        private SemaphoreSlim _canDequeue = new SemaphoreSlim(0);

        public BuildRequestQueue(ILogger<BuildRequestQueue> logger)
        {
            _logger = logger;
        }

        public void QueueBuildRequest(BuildRequest buildRequest)
        {
            if(!_buildRequests.TryAdd(buildRequest))
            {
                _logger.LogDebug($"New build request ignored, there are already waiting build requests in the queue");
                return;
            }

            _logger.LogInformation($"Queued new build request due to: {buildRequest.FileSystemEventArgs.FullPath}");

            _canDequeue.Release();
         }

        public async Task<BuildRequest> DequeueBuildRequestAsync(CancellationToken cancellationToken)
        {
            await _canDequeue.WaitAsync(cancellationToken);
            await Task.Delay(500, cancellationToken);

            if(!_buildRequests.TryTake(out var buildRequest))
                return null;

            return buildRequest;
        }
    }
}
