using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Gaius.Core.Configuration
{
    public class GaiusConfiguration
    {
        private const string FILE_PROCESSOR = "file";
        public const string MARKDOWN_LIQUIDE_PIPELINE = "markdown-liquid";
        public string SourceDirectoryName { get; set; } = "_source";
        public string LayoutDirectoryName { get; set; } = "_layouts";
        public string GenerationDirectoryName { get; set; } = "_generated";
        public string Processor { get; set; } = FILE_PROCESSOR;
        public string Pipeline { get; set; } = MARKDOWN_LIQUIDE_PIPELINE;
        public string SiteContainerPath { get; set; }

        [JsonIgnore]
        public string GenerationDirectoryFullPath => Path.Combine(SiteContainerPath, GenerationDirectoryName);
        [JsonIgnore]
        public string SourceDirectoryFullPath => Path.Combine(SiteContainerPath, SourceDirectoryName);
        [JsonIgnore]
        public string LayoutDirectorFullPath => Path.Combine(SiteContainerPath, SourceDirectoryName, LayoutDirectoryName);

        [JsonIgnore]
        public List<string> SupportedProcessors => new List<string>() { FILE_PROCESSOR };
        [JsonIgnore]
        public List<string> SupportedPipelines => new List<string>() { MARKDOWN_LIQUIDE_PIPELINE };
    }
}