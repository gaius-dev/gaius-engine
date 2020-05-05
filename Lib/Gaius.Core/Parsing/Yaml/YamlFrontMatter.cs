using Gaius.Core.Models;

namespace Gaius.Core.Parsing.Yaml
{
    public class YamlFrontMatter : IFrontMatter
    {
        public string Layout { get; set; }
        public string Title { get;set; }
        public string Author { get; set; }
        public string Keywords { get;set; }
        public string Description { get;set; }
    }
}