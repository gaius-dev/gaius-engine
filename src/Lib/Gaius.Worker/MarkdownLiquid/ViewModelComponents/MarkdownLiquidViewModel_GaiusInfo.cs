using Gaius.Worker.Models;

namespace Gaius.Worker.MarkdownLiquid.ViewModelComponents
{
    public class MarkdownLiquidViewModel_GaiusInfo 
    {
        public MarkdownLiquidViewModel_GaiusInfo(GaiusInformation gaiusInformation)
        {
            version = gaiusInformation.Version;
        }

        public string version { get; set; }
    }
}