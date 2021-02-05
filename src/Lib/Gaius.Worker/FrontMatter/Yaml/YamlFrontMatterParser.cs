using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Gaius.Worker.FrontMatter.Yaml
{
    public class YamlFrontMatterParser : IFrontMatterParser
    {
        private static readonly IDeserializer _yamlDeserializer 
            = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

        private const string YAML_FRONTMATTER_START = "---";
        private const string YAML_FRONTMATTER_END = "...";

        public IFrontMatter DeserializeFromContent(string markdownContent)
        {
            var yamlContent = GetYamlContentFromMarkdownContent(markdownContent);

            if(string.IsNullOrEmpty(yamlContent))
                return null;

            return _yamlDeserializer.Deserialize<YamlFrontMatter>(yamlContent);
        }

        private static string GetYamlContentFromMarkdownContent(string markdownContent)
        {
            var indexStartYaml = markdownContent.IndexOf(YAML_FRONTMATTER_START);
            var indexEndYaml = markdownContent.IndexOf(YAML_FRONTMATTER_END);

            if(indexStartYaml == -1 || indexEndYaml == -1)
                return null;

            var yamlLength = indexEndYaml - indexStartYaml + YAML_FRONTMATTER_END.Length;
            var yamlContent = markdownContent.Substring(indexStartYaml, yamlLength);
            return yamlContent;
        }
    }
}