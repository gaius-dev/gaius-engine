using System.IO;
using Gaius.Core.Worker;
using Gaius.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSInfo
    {
        private readonly IWorker _worker;
        private readonly GaiusConfiguration _gaiusConfig;

        public FSInfo(IWorker worker, IOptions<GaiusConfiguration> gaiusConfig, FileSystemInfo fileSystemInfo)
        {
            _worker = worker;
            _gaiusConfig = gaiusConfig.Value;

            FileSystemInfo = fileSystemInfo;
            MetaInfo = _worker.GetWorkerMetaInfo(FileSystemInfo);
        }

        public static FSInfo CreateInstance(IServiceProvider provider, FileSystemInfo fileSystemInfo)
        {
            return ActivatorUtilities.CreateInstance<FSInfo>(provider, fileSystemInfo);
        }

        public FileSystemInfo FileSystemInfo { get; private set; }
        public DirectoryInfo DirectoryInfo => FileSystemInfo as DirectoryInfo;
        public FileInfo FileInfo => FileSystemInfo as FileInfo;
        public WorkerMetaInfo MetaInfo { get; private set; }
    }
}