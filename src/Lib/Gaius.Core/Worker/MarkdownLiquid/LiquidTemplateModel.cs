using Gaius.Core.Configuration;
using Gaius.Core.Models;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class LiquidTemplateModel
    {
        public LiquidTemplateModel(IFrontMatter frontMatter, PageData pageData, GaiusConfiguration gaiusConfiguration, GenerationInfo generationInfo)
        {
            title = frontMatter.Title;
            author = frontMatter.Author;
            keywords = frontMatter.Keywords;
            description = frontMatter.Description;
            draft = frontMatter.Draft;
            pageId = pageData.Id;
            content = pageData.Html;
            root = gaiusConfiguration.IsTestMode ? string.Empty : gaiusConfiguration.GenerationUrlRootPrefix;
            
            gaius = new LiquidTemplateModel_GaiusInfo()
            {
                version = generationInfo.GaiusVersion
            };
        }

        public string title { get;set; }
        public string author { get; set; }
        public string keywords { get;set; }
        public string description { get;set; }
        public bool draft { get; set; }
        public string pageId { get; set; }
        public string content { get; set; }
        public string root { get; set; }
        public LiquidTemplateModel_GaiusInfo gaius { get; set; }
    }

    public class LiquidTemplateModel_GaiusInfo 
    {
        public string version { get; set;}
    }
}