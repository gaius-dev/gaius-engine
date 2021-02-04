using System.Collections.Generic;
using System.Linq;
using Gaius.Worker.Models;

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
        public List<TagData> GetTagData()
        {
            if(Tags == null && !string.IsNullOrWhiteSpace(Tag))
                return new List<TagData>();

            var tags = new List<string>();

            if(Tags != null)
                tags.AddRange(Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
            
            if(!string.IsNullOrWhiteSpace(Tag))
                tags.Add(Tag);

            return tags.Select(tag => new TagData(tag)).ToList();
        }
    }
}