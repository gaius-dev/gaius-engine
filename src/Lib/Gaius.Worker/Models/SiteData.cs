
using System;
using Gaius.Core.Configuration;

namespace Gaius.Worker.Models
{
    public class SiteData
    {
        public SiteData(GaiusConfiguration gaiusConfiguration)
        {
            Url = gaiusConfiguration.GetGenerationUrlRootPrefix();
            Time = DateTime.UtcNow.ToString("u");
        }

        public string Url { get; private set; }
        public string Time { get; private set; }
    }
}