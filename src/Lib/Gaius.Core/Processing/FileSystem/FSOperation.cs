using Gaius.Core.Configuration;
using Gaius.Utilities.FileSystem;
using Gaius.Core.Worker;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSOperation
    {
        private readonly GaiusConfiguration _gaiusConfiguration;

        public FSOperation(WorkerTask workerTask, FSOperationType fsAction, GaiusConfiguration gaiusConfiguration)
        {
            _gaiusConfiguration = gaiusConfiguration;

            WorkerTask = workerTask;
            FSOperationType = fsAction;
            Status = OperationStatus.Pending;
        }

        /*
        public static FSOperation CreateInstance(IServiceProvider provider, WorkerTask fsInfo, FSOperationType fSAction)
        {
            return ActivatorUtilities.CreateInstance<FSOperation>(provider, fsInfo, fSAction);
        }
        */

        public string Name
        {
            get
            {
                //rs: override the operation name for the named theme directory (this is used when displaying the operation)
                if (WorkerTask.FileSystemInfo.IsDirectory() && WorkerTask.FileSystemInfo.FullName.Equals(_gaiusConfiguration.NamedThemeDirectoryFullPath))
                    return $"{_gaiusConfiguration.ThemesDirectoryName}/{WorkerTask.FileSystemInfo.Name}";

                return WorkerTask.FileSystemInfo.Name;
            }
        }

        public WorkerTask WorkerTask { get; private set; }
        public FSOperationType FSOperationType { get; private set; }
        public OperationStatus Status { get; set; }
        public bool IsUnsafe => FSOperationType == FSOperationType.Delete;
        public bool IsWorkerOmittedForOp => FSOperationType == FSOperationType.Skip 
                                            || FSOperationType == FSOperationType.Keep
                                            || FSOperationType == FSOperationType.Delete
                                            || FSOperationType == FSOperationType.SkipDelete
                                            || FSOperationType == FSOperationType.SkipDraft;
        public bool IsDirectoryOp => WorkerTask.FileSystemInfo.IsDirectory();
        public bool IsListingOp => WorkerTask.IsListing;
    }
}