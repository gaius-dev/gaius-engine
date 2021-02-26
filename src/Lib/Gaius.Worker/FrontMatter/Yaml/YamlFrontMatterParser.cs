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

        private const string _yamlFrontMatterStart = "---";
        private const string _yamlFrontMatterEnd = "...";

        public IFrontMatter DeserializeFromContent(string markdownContent)
        {
            var yamlContent = GetYamlContentFromMarkdownContent(markdownContent);

            if(string.IsNullOrEmpty(yamlContent))
                return null;

            return _yamlDeserializer.Deserialize<YamlFrontMatter>(yamlContent);
        }

        private static string GetYamlContentFromMarkdownContent(string markdownContent)
        {
            var indexStartYaml = markdownContent.IndexOf(_yamlFrontMatterStart);
            var indexEndYaml = markdownContent.IndexOf(_yamlFrontMatterEnd);

            if(indexStartYaml == -1 || indexEndYaml == -1)
                return null;

            var yamlLength = indexEndYaml - indexStartYaml + _yamlFrontMatterEnd.Length;
            var yamlContent = markdownContent.Substring(indexStartYaml, yamlLength);
            return yamlContent;
        }
    }
}