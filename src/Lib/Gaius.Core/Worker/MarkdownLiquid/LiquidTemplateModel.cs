using Gaius.Core.Configuration;
using Gaius.Core.Models;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class LiquidTemplateModel
    {
        public LiquidTemplateModel(IFrontMatter frontMatter, PageData pageData, GaiusConfiguration gaiusConfiguration, GenerationInfo generationInfo)
        {
            root = gaiusConfiguration.IsTestMode ? string.Empty : gaiusConfiguration.GenerationUrlRootPrefix;

            page = new LiquidTemplateModel_Page() 
            {
                id = pageData.Id,
                title = frontMatter.Title,
                author = frontMatter.Author,
                keywords = frontMatter.Keywords,
                description = frontMatter.Description,
                draft = frontMatter.Draft,
                content = pageData.Html
            };
            
            gaius = new LiquidTemplateModel_GaiusInfo()
            {
                version = generationInfo.GaiusVersion,
                gendate = generationInfo.GenerationDateTime.ToString("u")
            };
        }

        public string root { get; set; }
        public LiquidTemplateModel_Page page { get; set;}
        public LiquidTemplateModel_GaiusInfo gaius { get; set; }
    }

    public class LiquidTemplateModel_Page
    {
        public string id { get; set; }
        public string title { get;set; }
        public string author { get; set; }
        public string keywords { get;set; }
        public string description { get;set; }
        public bool draft { get; set; }
        public string content { get; set; }
    }

    public class LiquidTemplateModel_GaiusInfo 
    {
        public string version { get; set; }
        public string gendate { get; set; }
    }
}