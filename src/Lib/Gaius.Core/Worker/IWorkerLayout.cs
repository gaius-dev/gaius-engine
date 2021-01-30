namespace Gaius.Core.Worker
{
    public interface IWorkerLayout
    {
        string Id { get; }
        string PaginatorId { get; }
        string LayoutContent { get; }
        bool IsListing { get; }
        bool IsDefaultPostListing { get; }
    }
}