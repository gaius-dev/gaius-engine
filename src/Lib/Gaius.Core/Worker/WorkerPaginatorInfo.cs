namespace Gaius.Core.Worker
{
    public class WorkerPaginatorInfo
    {
        public string PaginatorId { get; private set; }
        public int PageNumber { get; private set; }
        public int PagesTotal { get; private set; }
        public bool HasNext => PageNumber < PagesTotal;
        public bool HasPrev => PageNumber > 1;
    }
}