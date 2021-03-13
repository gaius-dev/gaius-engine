using System.Collections.Generic;
using System.Linq;
using Gaius.Worker.Models;

namespace Gaius.Worker.MarkdownLiquid.ViewModelComponents
{
    public class MarkdownLiquidViewModel_Paginator
    {
        internal MarkdownLiquidViewModel_Paginator (ViewModel viewModelData, SiteData siteData)
        {
            page = viewModelData.Paginator.PageNumber;
            per_page = viewModelData.Paginator.ItemsPerPage;
            posts = viewModelData.PaginatorViewModels.Select(pgViewModel => new MarkdownLiquidViewModel_Page(pgViewModel, siteData, true)).ToList();
            total_posts = viewModelData.Paginator.TotalItems;
            total_pages = viewModelData.Paginator.TotalPages;
            previous_page = viewModelData.Paginator.PrevPageNumber;
            previous_page_path = viewModelData.Paginator.PrevPageUrl;
            next_page = viewModelData.Paginator.NextPageNumber;
            next_page_path = viewModelData.Paginator.NextPageUrl;

            var matchingSiteTagData = siteData.TagData.FirstOrDefault(td => td.Name.Equals(viewModelData.Paginator.AssociatedTagName));

            associated_tag = matchingSiteTagData != null
                                ? new MarkdownLiquidViewModel_Tag(matchingSiteTagData)
                                : null;
        }

        public int page { get; set; }
        public int per_page { get; set; }
        public List<MarkdownLiquidViewModel_Page> posts { get; set; }
        public int total_posts { get; set; }
        public int total_pages { get; set; }
        public int? previous_page { get; set; }
        public string previous_page_path { get; set; }
        public int? next_page { get; set; }
        public string next_page_path { get; set; }
        public MarkdownLiquidViewModel_Tag associated_tag { get; set; }
    }
}