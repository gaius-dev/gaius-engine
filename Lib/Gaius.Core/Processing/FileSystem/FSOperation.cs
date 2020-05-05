using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gaius.Core.Configuration;
using Strube.Utilities.FileSystem;
using Gaius.Core.Worker;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSOperation
    {
        private readonly IWorker _worker;
        private readonly GaiusConfiguration _gaiusConfig;

        public FSOperation(IWorker worker, IOptions<GaiusConfiguration> gaiusConfig, FileSystemInfo fsInfo, FSOperationType fsAction)
        {
            _worker = worker;
            _gaiusConfig = gaiusConfig.Value;

            FSInfo = fsInfo;
            FSOperationType = ShouldSkip(fsInfo) ? FSOperationType.Skip : fsAction;

            Status = OperationStatus.Pending;

            if(fsAction == FSOperationType.Delete || fsAction == FSOperationType.Skip)
                WorkerTask = null;

            else WorkerTask = _worker.GenerateWorkerTask(fsInfo);
        }

        public static FSOperation CreateInstance(IServiceProvider provider, FileSystemInfo fsInfo, FSOperationType fSAction)
        {
            return ActivatorUtilities.CreateInstance<FSOperation>(provider, fsInfo, fSAction);
        }

        public FileSystemInfo FSInfo { get; private set; }
        public FSOperationType FSOperationType { get; private set; }
        public WorkerTask WorkerTask { get; private set;}
        public OperationStatus Status { get; set; }
        public bool IsUnsafe => FSOperationType == FSOperationType.Delete;
        public bool IsDirectoryOp => FSInfo.IsDirectory();
        public bool ShouldSkip(FileSystemInfo fsInfo)
        {
            if(fsInfo.Name.Equals(_gaiusConfig.LayoutDirectoryName))
                return true;

            if(fsInfo.IsLiquidFile())
                return true;

            return false;
        }
    }
}