using Gaius.Utilities.FileSystem;
using Gaius.Core.Worker;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSOperation
    {
        public FSOperation(WorkerTask workerTask, bool isOpInGenDir = false) : this(workerTask, FSOperationType.Undefined, isOpInGenDir) { }
        public FSOperation(WorkerTask workerTask, FSOperationType fsOperationType, bool isOpInGenDir = false)
        {
            WorkerTask = workerTask;

            if(fsOperationType == FSOperationType.Undefined)
            {
                if(isOpInGenDir)
                    fsOperationType = WorkerTask.IsKeep ? FSOperationType.Keep : FSOperationType.Delete;

                else fsOperationType = WorkerTask.IsSkip ? FSOperationType.Skip : FSOperationType.CreateOverwrite;
            }

            FSOperationType = fsOperationType;
            Status = OperationStatus.Pending;
        }

        public WorkerTask WorkerTask { get; private set; }
        public FSOperationType FSOperationType { get; internal set; }
        public OperationStatus Status { get; internal set; }
        public bool IsEmptyOp => FSOperationType != FSOperationType.CreateOverwrite;
        public bool IsUnsafe => FSOperationType == FSOperationType.Delete;
        public bool IsDirectoryOp => WorkerTask.FileSystemInfo.IsDirectory();
    }
}