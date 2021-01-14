using System.IO;
using Gaius.Core.Models;
using Gaius.Core.Worker;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Gaius.Core.Parsing.Yaml
{
    public class YamlFrontMatterParser : IFrontMatterParser
    {
        private readonly IWorker _worker;
        
        public YamlFrontMatterParser(IWorker worker)
        {
            _worker = worker;
        }

        private static readonly IDeserializer _yamlDeserializer 
            = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

        private const string YAML_FRONTMATTER_START = "---";
        private const string YAML_FRONTMATTER_END = "...";

        public IFrontMatter GetFrontMatter(FileSystemInfo fileSystemInfo)
        {
            if(!_worker.HasFrontMatter(fileSystemInfo))
                return null;
            
            return DeserializeFromContent(File.ReadAllText(fileSystemInfo.FullName));
        }

        private static IFrontMatter DeserializeFromContent(string markdownContent)
        {
            var yamlContent = GetYamlContentFromMarkdownContent(markdownContent);
            var yamlFrontMatter = _yamlDeserializer.Deserialize<YamlFrontMatter>(yamlContent);
            return yamlFrontMatter;
        }

        private static string GetYamlContentFromMarkdownContent(string markdownContent)
        {
            var indexStartYaml = markdownContent.IndexOf(YAML_FRONTMATTER_START);
            var indexEndYaml = markdownContent.IndexOf(YAML_FRONTMATTER_END);
            var yamlLength = indexEndYaml - indexStartYaml + YAML_FRONTMATTER_END.Length;
            var yamlContent = markdownContent.Substring(indexStartYaml, yamlLength);
            return yamlContent;
        }
    }
}