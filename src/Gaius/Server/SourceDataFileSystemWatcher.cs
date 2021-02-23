using System.IO;
using System.Threading.Tasks;
using Gaius.Core.Configuration;
using Gaius.Server.BackgroundHostedService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gaius.Server
{
    public class SourceDataFileSystemWatcher
    {
        private readonly ILogger _logger;
        private readonly GaiusConfiguration _gaiusConfiguration;
        private readonly IBuildRequestQueue _buildRequestQueue;
        private FileSystemWatcher _fileSystemWatcher;

        public SourceDataFileSystemWatcher(ILogger<SourceDataFileSystemWatcher> logger,
                                           IOptions<GaiusConfiguration> gaiusConfigurationOptions,
                                           IBuildRequestQueue buildRequestQueue)
        {
            _logger = logger;
            _gaiusConfiguration = gaiusConfigurationOptions.Value;
            _buildRequestQueue = buildRequestQueue;
        }

        public void StartWatcher()
        {
            _logger.LogInformation($"Starting file system watcher for all data in {_gaiusConfiguration.SourceDirectoryFullPath}");

            Task.Run(() => Watch(_gaiusConfiguration.SourceDirectoryFullPath));
        }

        private void Watch(string sourceDirFullPath)
        {
            _fileSystemWatcher = new FileSystemWatcher(sourceDirFullPath)
            {
                Filter = string.Empty,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite
                                | NotifyFilters.FileName
                                | NotifyFilters.DirectoryName
            };

            _fileSystemWatcher.Changed += OnSourceDataChanged;
            _fileSystemWatcher.Created += OnSourceDataChanged;
            _fileSystemWatcher.Deleted += OnSourceDataChanged;
            _fileSystemWatcher.Renamed += OnSourceDataChanged;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void OnSourceDataChanged(object source, FileSystemEventArgs e)
        {
            _logger.LogDebug($"Source data changed: {e.FullPath} {e.ChangeType}");
            
            _buildRequestQueue.QueueBuildRequest(new BuildRequest(e));
        }
    }
}