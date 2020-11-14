using Gaius.Core.Configuration;
using Gaius.Core.Models;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class LiquidTemplateModel
    {
        public LiquidTemplateModel(string transformId, IFrontMatter frontMatter, string html, GaiusConfiguration gaiusConfiguration)
        {
            pageId = transformId;
            title = frontMatter.Title;
            author = frontMatter.Author;
            keywords = frontMatter.Keywords;
            description = frontMatter.Description;
            draft = frontMatter.Draft;
            content = html;
            root = gaiusConfiguration.IsTestCommand ? string.Empty : gaiusConfiguration.GenerationUrlRootPrefix;
        }

        public string pageId { get; set; }
        public string title { get;set; }
        public string author { get; set;}
        public string keywords { get;set; }
        public string description { get;set; }
        public bool draft { get; set; }
        public string content { get; set; }
        public string root { get; set;}
    }
}