using System.IO;
using Newtonsoft.Json;
using Gaius.Core.Arguments;

namespace Gaius.Core.Configuration
{
    public class GaiusConfiguration
    {
        public GaiusConfiguration(){ }
        public GaiusConfiguration(GaiusArguments gaiusArgs)
        {
            SiteContainerFullPath = gaiusArgs.BasePath;
            IsTestModeEnabled = gaiusArgs.IsTestModeEnabled;
        }
        
        public string SourceDirectoryName { get; set; } = "_source";
        public string GenerationDirectoryName { get; set; } = "_generated";
        public string ThemesDirectoryName { get; set; } = "_themes";
        public string PostsDirectoryName { get; set; } = "_posts";
        public string DraftsDirectoryName { get; set; } = "_drafts";
        public string PostUrlPrefix { get; set; } = "";
        public string TagListDirectoryName { get; set; } = "_taglist";
        public string TagUrlPrefix { get; set; } = "tag";
        public string ThemeName { get; set; } = "default";
        public int Pagination { get; set; } = 5;
        public string GenerationUrlRootPrefix { get; set; } = string.Empty;
        public string[] AlwaysKeep { get; set; }

        [JsonIgnore]
        public string SiteContainerFullPath { get; set; }

        [JsonIgnore]
        public bool IsTestModeEnabled { get; set; } = false;

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

        public string GetGenerationUrlRootPrefix() => IsTestModeEnabled ? string.Empty : GenerationUrlRootPrefix;
    }
}