using System.Collections.Generic;
using System.Linq;
using Gaius.Worker.Models;

namespace Gaius.Worker.MarkdownLiquid.ViewModelComponents
{
    public class MarkdownLiquidViewModel_Site
    {
        internal MarkdownLiquidViewModel_Site(SiteData siteData)
        {
            url = siteData.Url;
            time = siteData.Time;
            cachebust = siteData.CacheBust;
            nav = siteData.NavData?.Select(navDataItem => new MarkdownLiquidViewModel_Nav(navDataItem)).ToList();
            sidebar = siteData.SidebarData?.Select(sidebarDataItem => new MarkdownLiquidViewModel_Nav(sidebarDataItem)).ToList();
            tags = siteData.TagData?.Select(tag => new MarkdownLiquidViewModel_Tag(tag)).ToList();
        }

        public string url { get; set; }
        public string time { get; set; }
        public string cachebust { get; set; }
        public List<MarkdownLiquidViewModel_Nav> nav { get; set; }
        public List<MarkdownLiquidViewModel_Nav> sidebar { get; set; }
        public List<MarkdownLiquidViewModel_Tag> tags { get; set; }
    }
}