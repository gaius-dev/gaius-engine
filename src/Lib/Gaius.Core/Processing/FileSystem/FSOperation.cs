using Gaius.Utilities.FileSystem;
using Gaius.Core.Worker;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSOperation
    {
        public FSOperation(WorkerTask workerTask) : this(workerTask, FSOperationType.Undefined) { }
        public FSOperation(WorkerTask workerTask, FSOperationType fsOperationType)
        {
            WorkerTask = workerTask;

            if(fsOperationType == FSOperationType.Undefined)
            {
                if(WorkerTask.IsSkip)
                    fsOperationType = FSOperationType.Skip;

                else if(WorkerTask.IsKeep)
                    fsOperationType = FSOperationType.Keep;
            }

            FSOperationType = fsOperationType;
            Status = OperationStatus.Pending;
        }

        public WorkerTask WorkerTask { get; private set; }
        public FSOperationType FSOperationType { get; internal set; }
        public OperationStatus Status { get; internal set; }
        public bool IsEmptyOp => FSOperationType != FSOperationType.CreateNew
                                    && FSOperationType != FSOperationType.Overwrite;
        public bool IsUnsafe => FSOperationType == FSOperationType.Delete;
        public bool IsDirectoryOp => WorkerTask.FileSystemInfo.IsDirectory();
        public bool IsListingOp => WorkerTask.IsListing;
    }
}