using System.IO;
using Gaius.Core.Worker;
using Gaius.Core.Configuration;
using Microsoft.Extensions.Options;
using Gaius.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using Gaius.Core.Parsing;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSInfo
    {
        private readonly IWorker _worker;
        private readonly IFrontMatterParser _frontMatterParser;
        private readonly GaiusConfiguration _gaiusConfig;

        public FSInfo(IWorker worker, IFrontMatterParser frontMatterParser, IOptions<GaiusConfiguration> gaiusConfig, FileSystemInfo fileSystemInfo)
        {
            _worker = worker;
            _frontMatterParser = frontMatterParser;
            _gaiusConfig = gaiusConfig.Value;

            FileSystemInfo = fileSystemInfo;
            FrontMatter = _frontMatterParser.GetFrontMatter(FileSystemInfo);
            IsPost = _worker.IsPost(FileSystemInfo);
            ShouldSkip = _worker.ShouldSkip(FileSystemInfo);
            ShouldKeep = _worker.ShouldKeep(FileSystemInfo);
        }

        public static FSInfo CreateInstance(IServiceProvider provider, FileSystemInfo fileSystemInfo)
        {
            return ActivatorUtilities.CreateInstance<FSInfo>(provider, fileSystemInfo);
        }

        public FileSystemInfo FileSystemInfo { get; private set; }
        public DirectoryInfo DirectoryInfo => FileSystemInfo as DirectoryInfo;
        public IFrontMatter FrontMatter { get; private set; }
        public bool IsDraft => FrontMatter?.IsDraft ?? false;
        public bool IsPost { get; private set; }
        public bool ShouldSkip { get; private set; }
        public bool ShouldKeep { get; private set; }
        public bool ShouldSkipKeep => ShouldSkip && ShouldKeep;
    }
}