using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Gaius.Core.Configuration
{
    public class GaiusConfiguration
    {
        public const string _markdownLiquidWorker = "Gaius.Core.Worker.MarkdownLiquid.MarkdownLiquidWorker";
        public string SourceDirectoryName { get; set; } = "_source";
        public string GenerationDirectoryName { get; set; } = "_generated";
        public string ThemesDirectoryName { get; set; } = "_themes";
        public string PostsDirectoryName { get; set; } = "_posts";
        public string DraftsDirectoryName { get; set; } = "_drafts";
        public string TagListDirectoryName { get; set; } = "_taglist";
        public string TagUrlPrefix { get; set; } = "tag";
        public string ThemeName { get; set; } = "default";
        public int Pagination { get; set; } = 5;
        public string GenerationUrlRootPrefix { get; set; } = string.Empty;
        public List<string> AlwaysKeep { get; set; } = new List<string>{ ".git" };
        public string Worker { get; set; } = _markdownLiquidWorker;
        public string SiteContainerFullPath { get; set; }
        public bool IsTestMode { get; set; } = false;

        [JsonIgnore]
        public string GenerationDirectoryFullPath => Path.Combine(SiteContainerFullPath, GenerationDirectoryName);
        
        [JsonIgnore]
        public string SourceDirectoryFullPath => Path.Combine(SiteContainerFullPath, SourceDirectoryName);

        [JsonIgnore]
        public string NamedThemeDirectoryFullPath => Path.Combine(SiteContainerFullPath, ThemesDirectoryName, ThemeName);

        [JsonIgnore]
        public string PostsDirectoryFullPath => Path.Combine(SiteContainerFullPath, SourceDirectoryName, PostsDirectoryName);

        [JsonIgnore]
        public string DraftsDirectoryFullPath => Path.Combine(SiteContainerFullPath, SourceDirectoryName, DraftsDirectoryName);

        [JsonIgnore]
        public string TagListDirectoryFullPath => Path.Combine(SiteContainerFullPath, SourceDirectoryName, TagListDirectoryName);

        [JsonIgnore]
        public List<string> SupportedWorkers => new List<string>() { _markdownLiquidWorker };

        public string GetGenerationUrlRootPrefix() => IsTestMode ? string.Empty : GenerationUrlRootPrefix;
    }
}