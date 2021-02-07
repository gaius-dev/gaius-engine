namespace Gaius.Worker.Models
{
    public class Paginator
    {
        public Paginator(int itemsPerPage, int pageNumber, int totalPages, int totalItems, string associatedTag = null)
        {
            ItemsPerPage = itemsPerPage;
            PageNumber = pageNumber;
            TotalPages = totalPages;
            TotalItems = totalItems;
            AssociatedTagName = associatedTag;
        }
        public int ItemsPerPage { get; private set; }
        public int PageNumber { get; private set; }
        public int TotalPages { get; private set; }
        public int TotalItems { get; private set; }
        public string AssociatedTagName { get; private set; }
        public bool HasNext => PageNumber < TotalPages;
        public bool HasPrev => PageNumber > 1;
        public int? PrevPageNumber => HasPrev ? PageNumber - 1 : (int?)null;
        public int? NextPageNumber => HasNext ? PageNumber + 1 : (int?)null;
        public string PrevPageUrl { get; private set; }
        public string NextPageUrl { get; private set; }
        public string OutputDisplayLabel 
            => string.IsNullOrEmpty(AssociatedTagName) 
                ? "[post list]"
                : $"[\"{AssociatedTagName}\" list]";

        public void AddPrevAndNextUrls(string prevUrl, string nextUrl)
        {
            PrevPageUrl = prevUrl;
            NextPageUrl = nextUrl;
        }
    }
}