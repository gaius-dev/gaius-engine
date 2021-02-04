using System;
using System.Collections.Generic;
using System.Linq;
using Gaius.Worker.Models;

namespace Gaius.Worker.MarkdownLiquid
{
    public class MarkdownLiquidViewModel
    {
        public MarkdownLiquidViewModel(ViewModel viewModelData, SiteData siteData, GaiusInformation gaiusInformation)
        {
            page = new MarkdownLiquidViewModel_Page(viewModelData);

            if(viewModelData.Paginator != null && viewModelData.PaginatorViewModels?.Count > 0)
                paginator = new MarkdownLiquidViewModel_Paginator(viewModelData);
            
            site = new MarkdownLiquidViewModel_Site() 
            {
                url = siteData.Url,
                time = siteData.Time
            };

            gaius = new MarkdownLiquidViewModel_GaiusInfo()
            {
                version = gaiusInformation.Version
            };
        }

        public MarkdownLiquidViewModel_Page page { get; set; }
        public MarkdownLiquidViewModel_Paginator paginator { get; set; }
        public MarkdownLiquidViewModel_Site site { get; set; }
        public MarkdownLiquidViewModel_GaiusInfo gaius { get; set; }
    }

    public class MarkdownLiquidViewModel_Page
    {
        public MarkdownLiquidViewModel_Page(BaseViewModel baseViewModel, bool generateTeaser = false)
        {
            id = baseViewModel.Id;
            url = baseViewModel.Url;
            title = baseViewModel.FrontMatter.Title;
            author = baseViewModel.FrontMatter.Author;
            keywords = baseViewModel.FrontMatter.Keywords;
            description = baseViewModel.FrontMatter.Description;
            tag = baseViewModel.FrontMatter.Tag;
            tags = baseViewModel.FrontMatter.Tags;
            content = baseViewModel.Content;

            if(generateTeaser)
                teaser = GenerateTeaser(content);
        }

        public string id { get; private set; }
        public string url { get; private set; }
        public string title { get; private set; }
        public string author { get; private set; }
        public string keywords { get; private set; }
        public string description { get; private set; }
        public string content { get; private set; }
        public string teaser { get; private set; }
        public string tag { get; private set; }
        public List<string> tags { get; private set; }

        private static string GenerateTeaser(string content)
        {
            var numberOfParagraphsToExtract = 2;

            var indexOfFirstOpeningP = content.IndexOf("<p>", StringComparison.InvariantCultureIgnoreCase);
            var indexOfClosingP = indexOfFirstOpeningP;

            for(var p=0; p < numberOfParagraphsToExtract; p++)
            {
                indexOfClosingP = content.IndexOf("</p>", indexOfClosingP + 1, StringComparison.InvariantCultureIgnoreCase);
            }

            return content.Substring(indexOfFirstOpeningP, indexOfClosingP + 4 - indexOfFirstOpeningP);
        }
    }

    public class MarkdownLiquidViewModel_Paginator
    {
        public MarkdownLiquidViewModel_Paginator (ViewModel viewModelData)
        {
            page = viewModelData.Paginator.PageNumber;
            per_page = viewModelData.Paginator.ItemsPerPage;
            posts = viewModelData.PaginatorViewModels.Select(pgViewModel => new MarkdownLiquidViewModel_Page(pgViewModel, true)).ToList();
            total_posts = viewModelData.Paginator.TotalItems;
            total_pages = viewModelData.Paginator.TotalPages;
            previous_page = viewModelData.Paginator.PrevPageNumber;
            previous_page_path = viewModelData.Paginator.PrevPageUrl;
            next_page = viewModelData.Paginator.NextPageNumber;
            next_page_path = viewModelData.Paginator.NextPageUrl;
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
    }

    public class MarkdownLiquidViewModel_Site
    {
        public string url { get; set; }
        public string time { get; set; }
    }

    public class MarkdownLiquidViewModel_GaiusInfo 
    {
        public string version { get; set; }
    }
}