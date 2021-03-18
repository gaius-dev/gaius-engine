using Gaius.Core.FileSystem;
using Gaius.Worker;

namespace Gaius.Processing.FileSystem
{
    public class FSOperation : IOperation
    {
        private string _nullOpSourceDisplay;
        private string _nullOpOutputDisplay;
        public FSOperation(string nullOpSourceDisplay, string nullOpOutputDisplay)
        {
            OperationType = OperationType.Null;
            Status = OperationStatus.Pending;
            _nullOpSourceDisplay = nullOpSourceDisplay;
            _nullOpOutputDisplay = nullOpOutputDisplay;
        }
        public FSOperation(WorkerTask workerTask) : this(workerTask, OperationType.Null) { }
        public FSOperation(WorkerTask workerTask, OperationType operationType)
        {
            WorkerTask = workerTask;

            if(operationType == OperationType.Null)
                operationType = AutoDetectOperationTypeFromWorkerTask(workerTask);

            OperationType = operationType;
            Status = OperationStatus.Pending;
        }

        public WorkerTask WorkerTask { get; private set; }
        public OperationType OperationType { get; private set; }
        public OperationStatus Status { get; set; }
        public bool IsNullOp => OperationType == OperationType.Null;
        public bool NoActionRequired => OperationType != OperationType.CreateOverwrite;
        public bool IsInvalid => OperationType == OperationType.Invalid;
        public bool IsDirectoryOp => WorkerTask?.FileSystemInfo?.IsDirectory() ?? false;
        public string SourceDisplay => string.IsNullOrEmpty(_nullOpSourceDisplay) ? WorkerTask.SourceDisplay : _nullOpSourceDisplay;
        public string OutputDisplay => string.IsNullOrEmpty(_nullOpOutputDisplay) ? WorkerTask.OutputDisplay : _nullOpOutputDisplay;
        
        private static OperationType AutoDetectOperationTypeFromWorkerTask(WorkerTask workerTask)
        {
            if(workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return OperationType.Invalid;

            return workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsSkip) ? OperationType.Skip : OperationType.CreateOverwrite;
        }
    }
}