using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gaius.Core.Configuration;
using Gaius.Utilities.FileSystem;
using Gaius.Core.Worker;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSOperation
    {
        private readonly IWorker _worker;
        private readonly GaiusConfiguration _gaiusConfig;

        public FSOperation(IWorker worker, IOptions<GaiusConfiguration> gaiusConfig, FSInfo fsInfo, FSOperationType fsAction)
        {
            _worker = worker;
            _gaiusConfig = gaiusConfig.Value;

            FSInfo = fsInfo;
            FSOperationType = fsAction;
            Status = OperationStatus.Pending;

            if(!IsWorkerOmittedForOp)
                WorkerTask = _worker.GenerateWorkerTask(fsInfo);
        }

        public static FSOperation CreateInstance(IServiceProvider provider, FSInfo fsInfo, FSOperationType fSAction)
        {
            return ActivatorUtilities.CreateInstance<FSOperation>(provider, fsInfo, fSAction);
        }

        public string Name
        {
            get
            {
                //rs: override the operation name for the named theme directory (this is used when displaying the operation)
                if (FSInfo.FileSystemInfo.IsDirectory() && FSInfo.FileSystemInfo.FullName.Equals(_gaiusConfig.NamedThemeDirectoryFullPath))
                    return $"{_gaiusConfig.ThemesDirectoryName}/{FSInfo.FileSystemInfo.Name}";

                return FSInfo.FileSystemInfo.Name;
            }
        }

        public FSInfo FSInfo { get; private set; }
        public FSOperationType FSOperationType { get; private set; }
        public WorkerTask WorkerTask { get; private set;}
        public OperationStatus Status { get; set; }
        public bool IsUnsafe => FSOperationType == FSOperationType.Delete;
        public bool IsWorkerOmittedForOp => FSOperationType == FSOperationType.Skip 
                                            || FSOperationType == FSOperationType.Keep
                                            || FSOperationType == FSOperationType.Delete
                                            || FSOperationType == FSOperationType.SkipDelete
                                            || FSOperationType == FSOperationType.SkipDraft;
        public bool IsDirectoryOp => FSInfo.FileSystemInfo.IsDirectory();
    }
}