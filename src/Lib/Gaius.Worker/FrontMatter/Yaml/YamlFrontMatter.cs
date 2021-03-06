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

        //main navigation
        [YamlMember(Alias = "nav_title", ApplyNamingConventions = false)]
        public string NavTitle { get; internal set; }

        [YamlMember(Alias = "nav_order", ApplyNamingConventions = false)]
        public string NavOrder { get; internal set; }
        public int NavLevel => GetLevelFromOrder(NavOrder);

        //sidebar
        [YamlMember(Alias = "sidebar_title", ApplyNamingConventions = false)]
        public string SidebarTitle { get; internal set; }

        [YamlMember(Alias = "sidebar_order", ApplyNamingConventions = false)]
        public string SidebarOrder { get; internal set; }
        public int SidebarLevel => GetLevelFromOrder(SidebarOrder);

        public string GetOrder(bool forSidebar) => forSidebar ? SidebarOrder : NavOrder;
        public int GetLevel(bool forSidebar) => forSidebar ? SidebarLevel : NavLevel;

        //tags
        public List<string> Tags { get; internal set; }
        public List<string> GetTags()
        {
            if(Tags == null)
                return new List<string>();

            var tags = Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList();
            
            return tags;
        }

        private static int GetLevelFromOrder(string orderStr)
        {
            if(string.IsNullOrWhiteSpace(orderStr))
                    return -1;

            return orderStr.Split('.', StringSplitOptions.RemoveEmptyEntries).Length - 1;
        }
    }
}