
using System;
using System.Collections.Generic;
using Gaius.Core.Configuration;

namespace Gaius.Worker.Models
{
    internal class SiteData
    {
        internal SiteData(GaiusConfiguration gaiusConfiguration)
        {
            Url = gaiusConfiguration.GetGenerationUrlRootPrefix();

            var now = DateTime.UtcNow;
            Time = now.ToString("u");
            CacheBust = now.Ticks.ToString();
        }

        internal void SetTagData(List<TagData> tagData)
        {
            TagData = tagData;
        }

        internal void SetNavData(List<BaseNavData> navData)
        {
            NavData = navData;
        }

        internal void SetSidebarData(List<BaseNavData> sidebarData)
        {
            SidebarData = sidebarData;
        }
        
        internal string Url { get; private set; }
        internal string Time { get; private set; }
        internal string CacheBust { get; private set; }
        internal List<TagData> TagData { get; private set; }
        internal List<BaseNavData> NavData { get; private set; }
        internal List<BaseNavData> SidebarData { get; private set; }
    }
}