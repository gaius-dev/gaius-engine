using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace Gaius.Worker.FrontMatter.Yaml
{
    public class YamlFrontMatter : IFrontMatter
    {
        public bool Draft { get; internal set; }
        public string Layout { get; internal set; }
        public string Title { get; internal set; }
        public string Author { get; internal set; }
        
        [YamlMember(Alias = "author_page", ApplyNamingConventions = false)]
        public string AuthorPage { get; internal set; }

        [YamlMember(Alias = "author_image", ApplyNamingConventions = false)]
        public string AuthorImage { get; internal set; }
        
        public string Keywords { get; internal set; }
        public string Description { get; internal set; }

        [YamlMember(Alias = "teaser_image", ApplyNamingConventions = false)]
        public string TeaserImage { get; internal set; }
        
        public string Image { get; internal set; }


        [YamlMember(Alias = "nav_order", ApplyNamingConventions = false)]
        public string NavOrder { get; internal set; }

        public int NavLevel
        {
            get
            {
                if(string.IsNullOrWhiteSpace(NavOrder))
                    return -1;

                return NavOrder.Split('.', StringSplitOptions.RemoveEmptyEntries).Length - 1;
            }
        }

        [YamlMember(Alias = "nav_title", ApplyNamingConventions = false)]
        public string NavTitle { get; internal set; }

        [YamlMember(Alias = "nav_in_header", ApplyNamingConventions = false)]
        public bool NavInHeader { get; internal set; }

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