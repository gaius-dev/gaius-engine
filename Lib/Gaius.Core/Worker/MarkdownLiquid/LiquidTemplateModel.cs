using Gaius.Core.Models;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class LiquidTemplateModel
    {
        public LiquidTemplateModel(IFrontMatter frontMatter, string html)
        {
            title = frontMatter.Title;
            author = frontMatter.Author;
            keywords = frontMatter.Keywords;
            description = frontMatter.Description;
            content = html;
        }

        public string title { get;set; }
        public string author { get; set;}
        public string keywords { get;set; }
        public string description { get;set; }
        public string content { get; set; }
    }
}