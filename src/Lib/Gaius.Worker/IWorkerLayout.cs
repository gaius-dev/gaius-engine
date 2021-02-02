namespace Gaius.Worker
{
    public interface IWorkerLayout
    {
        string Id { get; }
        string PaginatorId { get; }
        string LayoutContent { get; }
        bool IsPostListing { get; }
    }
}