using Gaius.Core.Configuration;
using Gaius.Core.Models;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidViewModel
    {
        public MarkdownLiquidViewModel(ViewModelData viewModelData, GaiusConfiguration gaiusConfiguration)
        {
            page = new MarkdownLiquidViewModel_Page() 
            {
                id = viewModelData.Id,
                url = viewModelData.Url,
                title = viewModelData.FrontMatter.Title,
                author = viewModelData.FrontMatter.Author,
                keywords = viewModelData.FrontMatter.Keywords,
                description = viewModelData.FrontMatter.Description,
                draft = viewModelData.FrontMatter.IsDraft,
                content = viewModelData.Html
            };
            
            site = new MarkdownLiquidViewModel_Site() 
            {
                url = gaiusConfiguration.GetGenerationUrlRootPrefix(),
                time = viewModelData.GenerationInfo.GenerationDateTime.ToString("u")
            };

            gaius = new MarkdownLiquidViewModel_GaiusInfo()
            {
                version = viewModelData.GenerationInfo.GaiusVersion
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