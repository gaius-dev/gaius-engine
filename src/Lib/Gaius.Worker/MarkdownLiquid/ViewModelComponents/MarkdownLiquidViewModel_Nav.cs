using System.Collections.Generic;
using System.Linq;
using Gaius.Worker.Models;

namespace Gaius.Worker.MarkdownLiquid.ViewModelComponents
{
    public class MarkdownLiquidViewModel_Nav
    {
        internal MarkdownLiquidViewModel_Nav(BaseNavData baseNavData)
        {
            id = baseNavData.Id;
            title = baseNavData.Title;
            url = baseNavData.Url ?? "#";
            order = baseNavData.Order;
            level = baseNavData.Level;
            
            children = baseNavData.Children != null && baseNavData.Children.Count > 0 
                        ? baseNavData.Children.Select(bnd => new MarkdownLiquidViewModel_Nav(bnd)).ToList()
                        : new List<MarkdownLiquidViewModel_Nav>();
        }

        public string id { get; set;}
        public string title { get; set; }
        public string url { get; set; }
        public int level { get; set; }
        public string order { get; set; }
        public List<MarkdownLiquidViewModel_Nav> children { get; set;}
    }
}