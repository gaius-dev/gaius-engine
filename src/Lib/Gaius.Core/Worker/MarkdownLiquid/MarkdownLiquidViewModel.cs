using Gaius.Core.Configuration;
using Gaius.Core.Models;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidViewModel
    {
        public MarkdownLiquidViewModel(PageData pageData, GenerationInfo generationInfo, GaiusConfiguration gaiusConfiguration)
        {
            page = new MarkdownLiquidViewModel_Page() 
            {
                id = pageData.Id,
                url = pageData.Url,
                title = pageData.FrontMatter.Title,
                author = pageData.FrontMatter.Author,
                keywords = pageData.FrontMatter.Keywords,
                description = pageData.FrontMatter.Description,
                draft = pageData.FrontMatter.IsDraft,
                content = pageData.Html
            };
            
            site = new MarkdownLiquidViewModel_Site() 
            {
                url = gaiusConfiguration.GetGenerationUrlRootPrefix(),
                time = generationInfo.GenerationDateTime.ToString("u")
            };

            gaius = new MarkdownLiquidViewModel_GaiusInfo()
            {
                version = generationInfo.GaiusVersion
            };
        }

        public MarkdownLiquidViewModel_Page page { get; set;}
        public MarkdownLiquidViewModel_Site site { get; set; }
        public MarkdownLiquidViewModel_GaiusInfo gaius { get; set; }
    }

    public class MarkdownLiquidViewModel_Page
    {
        public string id { get; set; }
        public string url { get; set; }
        public string title { get;set; }
        public string author { get; set; }
        public string keywords { get;set; }
        public string description { get;set; }
        public bool draft { get; set; }
        public string content { get; set; }
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