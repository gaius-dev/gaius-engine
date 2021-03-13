using Gaius.Worker.Models;

namespace Gaius.Worker.MarkdownLiquid.ViewModelComponents
{
    public class MarkdownLiquidViewModel_Tag
    {
        internal MarkdownLiquidViewModel_Tag(TagData tagData)
        {
            name = tagData.Name;
            url = tagData.Url ?? "#";
        }

        public string name { get; set; }
        public string url { get; set; }
    }
}