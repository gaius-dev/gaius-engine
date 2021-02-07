using System.Collections.Generic;
using System.Linq;

namespace Gaius.Worker.FrontMatter.Yaml
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
        public List<string> GetTags()
        {
            if(Tags == null && !string.IsNullOrWhiteSpace(Tag))
                return new List<string>();

            var tags = new List<string>();

            if(Tags != null)
                tags.AddRange(Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
            
            if(!string.IsNullOrWhiteSpace(Tag))
                tags.Add(Tag);

            return tags;
        }
    }
}