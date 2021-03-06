using System.IO;
using System.Text.RegularExpressions;
using Gaius.Core.Configuration;
using Gaius.Core.FileSystem;

namespace Gaius.Worker.MarkdownLiquid
{
    public class MarkdownLiquidLayout : IWorkerLayout
    {
        public MarkdownLiquidLayout(FileInfo fileInfo, GaiusConfiguration gaiusConfiguration)
        {
            Id = fileInfo.GetNameWithoutExtension();
            LayoutContent = File.ReadAllText(fileInfo.FullName);
            IsPostListing = LayoutContentContainsPaginator(LayoutContent);
        }

        public string Id { get; private set; }
        public bool IsPostListing { get; private set; }
        public string LayoutContent { get; private set; }

        private const string _paginatorRegExStr = @"{.*paginator\..*}";
        private static Regex _paginatorRegEx = new Regex(_paginatorRegExStr, RegexOptions.Compiled);

        private static bool LayoutContentContainsPaginator(string layoutContent)
        {
            return _paginatorRegEx.IsMatch(layoutContent);
        }
    }
}