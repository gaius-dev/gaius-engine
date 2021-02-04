using System.Collections.Generic;
using Gaius.Worker.Models;

namespace Gaius.Worker.Parsing.Yaml
{
    public class YamlFrontMatter : IFrontMatter
    {
        public string Layout { get; internal set; }
        public string Title { get; internal set; }
        public string Author { get; internal set; }
        public string Keywords { get; internal set; }
        public string Description { get; internal set; }
        public bool IsDraft { get; internal set; }
        public string Tag { get; internal set; }
        public List<string> Tags { get; internal set; }
    }
}