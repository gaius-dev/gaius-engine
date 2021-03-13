using Gaius.Worker.MarkdownLiquid.ViewModelComponents;
using Gaius.Worker.Models;

namespace Gaius.Worker.MarkdownLiquid
{
    public class MarkdownLiquidViewModel
    {
        internal MarkdownLiquidViewModel(ViewModel viewModelData, SiteData siteData, GaiusInformation gaiusInformation)
        {
            page = new MarkdownLiquidViewModel_Page(viewModelData, siteData);

            if(viewModelData.Paginator != null && viewModelData.PaginatorViewModels?.Count > 0)
                paginator = new MarkdownLiquidViewModel_Paginator(viewModelData, siteData);
            
            site = new MarkdownLiquidViewModel_Site(siteData);
            gaius = new MarkdownLiquidViewModel_GaiusInfo(gaiusInformation);
        }

        public MarkdownLiquidViewModel_Site site { get; set; }
        public MarkdownLiquidViewModel_Page page { get; set; }
        public MarkdownLiquidViewModel_Paginator paginator { get; set; }
        public MarkdownLiquidViewModel_GaiusInfo gaius { get; set; }
    }
}