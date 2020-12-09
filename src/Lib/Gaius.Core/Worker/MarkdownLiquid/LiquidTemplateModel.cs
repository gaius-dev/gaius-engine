using Gaius.Core.Configuration;
using Gaius.Core.Models;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class LiquidTemplateModel
    {
        public LiquidTemplateModel(PageData pageData, GenerationInfo generationInfo, GaiusConfiguration gaiusConfiguration)
        {
            page = new LiquidTemplateModel_Page() 
            {
                id = pageData.Id,
                url = pageData.Url,
                title = pageData.FrontMatter.Title,
                author = pageData.FrontMatter.Author,
                keywords = pageData.FrontMatter.Keywords,
                description = pageData.FrontMatter.Description,
                draft = pageData.FrontMatter.Draft,
                content = pageData.Html
            };
            
            site = new LiquidTemplateModel_Site() 
            {
                url = gaiusConfiguration.GetGenerationUrlRootPrefix(),
                time = generationInfo.GenerationDateTime.ToString("u")
            };

            gaius = new LiquidTemplateModel_GaiusInfo()
            {
                version = generationInfo.GaiusVersion
            };
        }

        public LiquidTemplateModel_Page page { get; set;}
        public LiquidTemplateModel_Site site { get; set; }
        public LiquidTemplateModel_GaiusInfo gaius { get; set; }
    }

    public class LiquidTemplateModel_Page
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

    public class LiquidTemplateModel_Site
    {
        public string url { get; set; }
        public string time { get; set; }
    }

    public class LiquidTemplateModel_GaiusInfo 
    {
        public string version { get; set; }
    }
}