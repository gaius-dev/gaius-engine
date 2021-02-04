namespace Gaius.Worker
{
    public interface IWorkerLayout
    {
        string Id { get; }
        string LayoutContent { get; }
        bool IsPostListing { get; }
    }
}