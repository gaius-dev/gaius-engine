using System.IO;
using Gaius.Utilities.FileSystem;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidLayoutInfo 
    {
        public MarkdownLiquidLayoutInfo(FileInfo fileInfo)
        {
            Id = fileInfo.GetNameWithoutExtension();
            LayoutContent = File.ReadAllText(fileInfo.FullName);

            //TODO: switch to RegEx
            ContainsPagination = LayoutContent.Contains("{{ paginator }}") || LayoutContent.Contains("{{paginator}}");

        }
        public string Id { get; private set; }
        public bool ContainsPagination { get; private set; }
        public string LayoutContent { get; private set; }
    }
}