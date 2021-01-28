namespace Gaius.Core.Worker
{
    public interface IWorkerLayoutData
    {
        string Id { get; }
        string PaginatorId { get; }
        string LayoutContent { get; }
        bool IsListing { get; }
        bool IsDefaultPostListing { get; }
    }
}