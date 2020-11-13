using Gaius.Core.Configuration;
using Gaius.Core.Models;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class LiquidTemplateModel
    {
        public LiquidTemplateModel(IFrontMatter frontMatter, string html, GaiusConfiguration gaiusConfiguration)
        {
            title = frontMatter.Title;
            author = frontMatter.Author;
            keywords = frontMatter.Keywords;
            description = frontMatter.Description;
            draft = frontMatter.Draft;
            content = html;
            rp = gaiusConfiguration.GenerationRootPrefix;
        }

        public string title { get;set; }
        public string author { get; set;}
        public string keywords { get;set; }
        public string description { get;set; }
        public bool draft { get; set; }
        public string content { get; set; }
        public string rp { get; set;}
    }
}