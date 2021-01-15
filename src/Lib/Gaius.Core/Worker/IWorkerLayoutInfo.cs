namespace Gaius.Core.Worker
{
    public interface IWorkerLayoutInfo
    {
        string Id { get; }
        string PaginatorId { get; }
        bool ContainsPaginator { get; }
        string LayoutContent { get; }
        bool IsPostListing => PaginatorId.Equals("post");
    }
}