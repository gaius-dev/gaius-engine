using Gaius.Worker;

namespace Gaius.Processing
{
    public interface IOperation
    {
        WorkerTask WorkerTask { get; }
        OperationType OperationType { get; }
        OperationStatus Status { get; }
        bool IsNullOp { get; }
        bool IsInvalid { get; }
        bool IsUnsafe { get; }
        bool IsDirectoryOp { get; }
        string SourceDisplay { get; }
        string OutputDisplay { get; }
    }
}