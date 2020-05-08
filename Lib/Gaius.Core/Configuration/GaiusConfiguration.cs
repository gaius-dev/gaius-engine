using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Gaius.Core.Configuration
{
    public class GaiusConfiguration
    {
        public const string MARKDOWN_LIQUID_PIPELINE = "Gaius.Core.Worker.MarkdownLiquid.MarkdownLiquidWorker";
        public string SourceDirectoryName { get; set; } = "_source";
        public string GenerationDirectoryName { get; set; } = "_generated";
        public string ThemesDirectoryName { get; set; } = "_themes";
        public string ThemeName { get; set; } = "default";
        public string Worker { get; set; } = MARKDOWN_LIQUID_PIPELINE;
        public string SiteContainerFullPath { get; set; }

        [JsonIgnore]
        public string GenerationDirectoryFullPath => Path.Combine(SiteContainerFullPath, GenerationDirectoryName);
        [JsonIgnore]
        public string SourceDirectoryFullPath => Path.Combine(SiteContainerFullPath, SourceDirectoryName);
        [JsonIgnore]
        public string NamedThemeDirectoryFullPath => Path.Combine(SiteContainerFullPath, ThemesDirectoryName, ThemeName);

        [JsonIgnore]
        public List<string> SupportedWorkers => new List<string>() { MARKDOWN_LIQUID_PIPELINE };
    }
}