using Gaius.Worker;

namespace Gaius.Processing
{
    public interface IOperation
    {
        WorkerTask WorkerTask { get; }
        OperationType OperationType { get; }
        OperationStatus Status { get; set; }
        bool IsNullOp { get; }
        bool NoActionRequired { get; }
        bool IsInvalid { get; }
        bool IsDirectoryOp { get; }
        string SourceDisplay { get; }
        string OutputDisplay { get; }
    }
}