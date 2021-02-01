using Gaius.Utilities.FileSystem;
using Gaius.Core.Worker;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSOperation
    {
        private string _nullOpSourceDisplay;
        private string _nullOpOutputDisplay;
        public FSOperation(string nullOpSourceDisplay, string nullOpOutputDisplay)
        {
            FSOperationType = FSOperationType.Null;
            Status = OperationStatus.Pending;
            _nullOpSourceDisplay = nullOpSourceDisplay;
            _nullOpOutputDisplay = nullOpOutputDisplay;
        }
        public FSOperation(WorkerTask workerTask) : this(workerTask, FSOperationType.Null) { }
        public FSOperation(WorkerTask workerTask, FSOperationType fsOperationType)
        {
            WorkerTask = workerTask;

            if(fsOperationType == FSOperationType.Null)
                fsOperationType = AutoDetectOperationTypeFromWorkerTask(workerTask);

            FSOperationType = fsOperationType;
            Status = OperationStatus.Pending;
        }

        public WorkerTask WorkerTask { get; private set; }
        public FSOperationType FSOperationType { get; internal set; }
        public OperationStatus Status { get; internal set; }
        public bool IsNullOp => FSOperationType == FSOperationType.Null;
        public bool NoFSActionRequired => FSOperationType != FSOperationType.CreateOverwrite;
        public bool IsInvalid => FSOperationType == FSOperationType.Invalid;
        public bool IsUnsafe => FSOperationType == FSOperationType.Delete;
        public bool IsDirectoryOp => WorkerTask?.FileSystemInfo?.IsDirectory() ?? false;
        public string SourceDisplay => string.IsNullOrEmpty(_nullOpSourceDisplay) ? WorkerTask.SourceDisplay : _nullOpSourceDisplay;
        public string OutputDisplay => string.IsNullOrEmpty(_nullOpOutputDisplay) ? WorkerTask.OutputDisplay : _nullOpOutputDisplay;
        
        private static FSOperationType AutoDetectOperationTypeFromWorkerTask(WorkerTask workerTask)
        {
            if(workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return FSOperationType.Invalid;

            if(workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsChildOfGenDir))
                return workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsKeep) ? FSOperationType.Keep : FSOperationType.Delete;

            return workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsSkip) ? FSOperationType.Skip : FSOperationType.CreateOverwrite;
        }
    }
}