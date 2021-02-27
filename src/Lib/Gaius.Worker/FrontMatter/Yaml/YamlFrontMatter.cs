using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace Gaius.Worker.FrontMatter.Yaml
{
    public class YamlFrontMatter : IFrontMatter
    {
        public string Layout { get; internal set; }
        public string Title { get; internal set; }
        public string Author { get; internal set; }
        
        [YamlMember(Alias = "author_page", ApplyNamingConventions = false)]
        public string AuthorPage { get; internal set; }

        [YamlMember(Alias = "author_image", ApplyNamingConventions = false)]
        public string AuthorImage { get; internal set; }
        
        public string Keywords { get; internal set; }
        public string Description { get; internal set; }
        public string Image { get; internal set; }
        public List<string> Tags { get; internal set; }
        public List<string> GetTags()
        {
            if(Tags == null)
                return new List<string>();

            var tags = Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList();
            
            return tags;
        }

    }
}