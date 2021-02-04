
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
            Time = DateTime.UtcNow.ToString("u");
        }

        internal void SetTagData(List<TagData> tags)
        {
            Tags = tags;
        }
        
        internal string Url { get; private set; }
        internal string Time { get; private set; }
        internal List<TagData> Tags { get; private set; }
    }
}