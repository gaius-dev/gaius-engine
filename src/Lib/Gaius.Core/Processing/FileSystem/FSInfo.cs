using System.IO;
using Gaius.Core.Worker;
using Gaius.Core.Configuration;
using Microsoft.Extensions.Options;
using Gaius.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSInfo
    {
        private readonly IWorker _worker;
        private readonly GaiusConfiguration _gaiusConfig;
        private readonly WorkerFSMetaInfo _fsMetaInfo;

        public FSInfo(IWorker worker, IOptions<GaiusConfiguration> gaiusConfig, FileSystemInfo fileSystemInfo)
        {
            _worker = worker;
            _gaiusConfig = gaiusConfig.Value;

            FileSystemInfo = fileSystemInfo;
            _fsMetaInfo = _worker.GetWorkerFSMetaInfo(FileSystemInfo);
        }

        public static FSInfo CreateInstance(IServiceProvider provider, FileSystemInfo fileSystemInfo)
        {
            return ActivatorUtilities.CreateInstance<FSInfo>(provider, fileSystemInfo);
        }

        public FileSystemInfo FileSystemInfo { get; private set; }
        public DirectoryInfo DirectoryInfo => FileSystemInfo as DirectoryInfo;
        public FileInfo FileInfo => FileSystemInfo as FileInfo;
        public IFrontMatter FrontMatter => _fsMetaInfo.FrontMatter;
        public bool IsDraft => FrontMatter?.IsDraft ?? false;
        public bool IsPost => _fsMetaInfo.IsPost;
        public bool IsPostListing => _fsMetaInfo.PaginatorIds.Contains("posts");
        public bool ShouldSkip => _fsMetaInfo.ShouldSkip;
        public bool ShouldKeep => _fsMetaInfo.ShouldKeep;
        public bool ShouldSkipKeep => ShouldSkip && ShouldKeep;
        public List<string> PaginatorIds => _fsMetaInfo.PaginatorIds;
        public bool ContainsPagination => _fsMetaInfo.ContainsPaginator;
    }
}