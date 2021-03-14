using System;
using System.Collections.Generic;
using System.Linq;
using Gaius.Worker.Models;

namespace Gaius.Worker.MarkdownLiquid.ViewModelComponents
{
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
            image = GetUrlFromFrontMatterRelativeUrl(siteData, baseViewModel.FrontMatter.Image);
            teaser_image = GetUrlFromFrontMatterRelativeUrl(siteData, baseViewModel.FrontMatter.TeaserImage) ?? image;
            nav_order = baseViewModel.FrontMatter.NavOrder;
            nav_level = baseViewModel.FrontMatter.NavLevel;
            sidebar_order = baseViewModel.FrontMatter.SidebarOrder;
            sidebar_level = baseViewModel.FrontMatter.SidebarLevel;
            content = baseViewModel.Content;

            if(generateTeaser)
                excerpt = GetExcerpt(content);

            var tagsFrontMatter = baseViewModel.FrontMatter?.GetTags();

            if(tagsFrontMatter != null)
            {
                var tagData = siteData.TagData.Where(td => tagsFrontMatter.Contains(td.Name));
                tags = tagData.Select(td => new MarkdownLiquidViewModel_Tag(td)).ToList();
            }

            else
                tags = new List<MarkdownLiquidViewModel_Tag>();
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
        public string sidebar_order { get; private set; }
        public int sidebar_level { get; private set; }
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
}