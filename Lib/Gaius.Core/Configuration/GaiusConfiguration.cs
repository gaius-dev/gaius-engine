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
        public string Worker { get; set; } = MARKDOWN_LIQUID_PIPELINE;
        public string SiteContainerPath { get; set; }

        [JsonIgnore]
        public string GenerationDirectoryFullPath => Path.Combine(SiteContainerPath, GenerationDirectoryName);
        [JsonIgnore]
        public string SourceDirectoryFullPath => Path.Combine(SiteContainerPath, SourceDirectoryName);

        [JsonIgnore]
        public List<string> SupportedWorkers => new List<string>() { MARKDOWN_LIQUID_PIPELINE };
    }
}