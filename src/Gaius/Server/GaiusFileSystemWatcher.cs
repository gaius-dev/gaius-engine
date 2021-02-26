using System.IO;
using System.Threading.Tasks;
using Gaius.Core.Configuration;
using Gaius.Server.BackgroundHostedService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gaius.Server
{
    public class GaiusFileSystemWatcher
    {
        private readonly ILogger _logger;
        private readonly GaiusConfiguration _gaiusConfiguration;
        private readonly IBuildRequestQueue _buildRequestQueue;
        private FileSystemWatcher _fileSystemWatcher;

        public GaiusFileSystemWatcher(ILogger<GaiusFileSystemWatcher> logger,
                                        IOptions<GaiusConfiguration> gaiusConfigurationOptions,
                                        IBuildRequestQueue buildRequestQueue)
        {
            _logger = logger;
            _gaiusConfiguration = gaiusConfigurationOptions.Value;
            _buildRequestQueue = buildRequestQueue;
        }

        public void StartWatcher()
        {
            _logger.LogInformation($"Starting file system watcher for all data in {_gaiusConfiguration.SiteContainerFullPath}");

            Task.Run(() => Watch(_gaiusConfiguration.SiteContainerFullPath));
        }

        private void Watch(string pathToWatch)
        {
            _fileSystemWatcher = new FileSystemWatcher(pathToWatch)
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
            if(!e.FullPath.Contains(_gaiusConfiguration.SourceDirectoryFullPath)
                && !e.FullPath.Contains(_gaiusConfiguration.NamedThemeDirectoryFullPath))
            {
                _logger.LogDebug($"Ignoring file system event: {e.FullPath} {e.ChangeType}");
                return;
            }

            _logger.LogDebug($"File system event: {e.FullPath} {e.ChangeType}");
            
            _buildRequestQueue.QueueBuildRequest(new BuildRequest(e));
        }
    }
}