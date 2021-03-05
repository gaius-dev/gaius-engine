using System;
using System.Collections.Generic;
using System.Linq;
using Gaius.Worker.Models;

namespace Gaius.Worker.MarkdownLiquid
{
    public class MarkdownLiquidViewModel
    {
        internal MarkdownLiquidViewModel(ViewModel viewModelData, SiteData siteData, GaiusInformation gaiusInformation)
        {
            page = new MarkdownLiquidViewModel_Page(viewModelData, siteData);

            if(viewModelData.Paginator != null && viewModelData.PaginatorViewModels?.Count > 0)
                paginator = new MarkdownLiquidViewModel_Paginator(viewModelData, siteData);
            
            site = new MarkdownLiquidViewModel_Site(siteData);
            gaius = new MarkdownLiquidViewModel_GaiusInfo(gaiusInformation);
        }

        public MarkdownLiquidViewModel_Site site { get; set; }
        public MarkdownLiquidViewModel_Page page { get; set; }
        public MarkdownLiquidViewModel_Paginator paginator { get; set; }
        public MarkdownLiquidViewModel_GaiusInfo gaius { get; set; }
    }

    public class MarkdownLiquidViewModel_Page
    {
        internal MarkdownLiquidViewModel_Page(BaseViewModel baseViewModel, SiteData siteData, bool generateTeaser = false)
        {
            id = baseViewModel.Id;
            url = baseViewModel.Url;
            date = baseViewModel.Date;
            title = baseViewModel.FrontMatter.Title;
            author = baseViewModel.FrontMatter.Author;
            author_page = GetUrlFromFrontMatterRelativeUrl(siteData, baseViewModel.FrontMatter.AuthorPage);
            author_image = GetUrlFromFrontMatterRelativeUrl(siteData, baseViewModel.FrontMatter.AuthorImage);
            keywords = baseViewModel.FrontMatter.Keywords;
            description = baseViewModel.FrontMatter.Description;
            teaser_image = GetUrlFromFrontMatterRelativeUrl(siteData, baseViewModel.FrontMatter.TeaserImage);
            image = GetUrlFromFrontMatterRelativeUrl(siteData, baseViewModel.FrontMatter.Image);
            nav_order = baseViewModel.FrontMatter.NavOrder;
            nav_level = baseViewModel.FrontMatter.NavLevel;

            var tagsFrontMatter = baseViewModel.FrontMatter?.GetTags();
            if(tagsFrontMatter != null)
            {
                var tagData = siteData.TagData.Where(td => tagsFrontMatter.Contains(td.Name));
                tags = tagData.Select(td => new MarkdownLiquidViewModel_Tag(td)).ToList();
            }

            content = baseViewModel.Content;

            if(generateTeaser)
                excerpt = GetExcerpt(content);
        }

        public string id { get; private set; }
        public string url { get; private set; }
        public string date { get; private set; }
        public string title { get; private set; }
        public string author { get; private set; }
        public string author_page { get; private set; }
        public string author_image { get; private set; }
        public string keywords { get; private set; }
        public string description { get; private set; }
        public string teaser_image { get; private set; }
        public string image { get; private set; }
        public string nav_order { get; private set; }
        public int nav_level { get; private set; }
        public string content { get; private set; }
        public string excerpt { get; private set; }
        public List<MarkdownLiquidViewModel_Tag> tags { get; private set; }
        private const string _moreComment = "<!--more-->";
        private const int _numberOfParagraphsToExtract = 1;
        private const string _pStart = "<p>";
        private const string _pEnd = "</p>";

        private static string GetUrlFromFrontMatterRelativeUrl(SiteData siteData, string relativeUrl)
        {
            if(string.IsNullOrWhiteSpace(relativeUrl))
                return null;

            if(string.IsNullOrWhiteSpace(siteData.Url))
                return relativeUrl;

            return $"{siteData.Url}{relativeUrl}";
        }

        private static string GetExcerpt(string content)
        {
            return GetExcerptBeforeMoreComment(content)
                   ?? GetExcerptFromParagraphs(content, _numberOfParagraphsToExtract);
        }

        private static string GetExcerptBeforeMoreComment(string content)
        {
            var indexOfMoreComment = content.IndexOf(_moreComment);

            if(indexOfMoreComment == -1)
                return null;

            return content.Substring(0, indexOfMoreComment);
        }

        private static string GetExcerptFromParagraphs(string content, int numberOfParagraphsToExtract)
        {
            var indexOfFirstOpeningP = content.IndexOf(_pStart, StringComparison.InvariantCultureIgnoreCase);
            var indexMarker = indexOfFirstOpeningP;

            for(var p=0; p < numberOfParagraphsToExtract; p++)
            {
                indexMarker = content.IndexOf(_pEnd, indexMarker + 1, StringComparison.InvariantCultureIgnoreCase);
            }

            return content.Substring(indexOfFirstOpeningP, indexMarker + _pEnd.Length - indexOfFirstOpeningP);
        }
    }

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

    public class MarkdownLiquidViewModel_Tag
    {
        internal MarkdownLiquidViewModel_Tag(TagData tagData)
        {
            name = tagData.Name;
            url = tagData.Url ?? "#";
        }

        public string name { get; set; }
        public string url { get; set; }
    }

    public class MarkdownLiquidViewModel_Nav
    {
        internal MarkdownLiquidViewModel_Nav(NavData navData)
        {
            id = navData.Id;
            title = navData.Title;
            url = navData.Url ?? "#";
            order = navData.Order;
            level = navData.Level;

            if(navData.Children != null && navData.Children.Count > 0)
                children = navData.Children.Select(nd => new MarkdownLiquidViewModel_Nav(nd)).ToList();
        }

        public string id { get; set;}
        public string title { get; set; }
        public string url { get; set; }
        public int level { get; set; }
        public string order { get; set; }
        public List<MarkdownLiquidViewModel_Nav> children { get; set;}
    }

    public class MarkdownLiquidViewModel_Site
    {
        internal MarkdownLiquidViewModel_Site(SiteData siteData)
        {
            url = siteData.Url;
            time = siteData.Time;
            nav = siteData.NavData?.Select(navDataItem => new MarkdownLiquidViewModel_Nav(navDataItem)).ToList();
            tags = siteData.TagData?.Select(tag => new MarkdownLiquidViewModel_Tag(tag)).ToList();
        }

        public string url { get; set; }
        public string time { get; set; }
        public List<MarkdownLiquidViewModel_Nav> nav { get; set;}
        public List<MarkdownLiquidViewModel_Tag> tags { get; set; }

    }

    public class MarkdownLiquidViewModel_GaiusInfo 
    {
        public MarkdownLiquidViewModel_GaiusInfo(GaiusInformation gaiusInformation)
        {
            version = gaiusInformation.Version;
        }

        public string version { get; set; }
    }
}