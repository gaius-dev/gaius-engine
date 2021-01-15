using System.IO;
using System.Text.RegularExpressions;
using Gaius.Utilities.FileSystem;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidLayoutInfo 
    {
        public MarkdownLiquidLayoutInfo(FileInfo fileInfo)
        {
            Id = fileInfo.GetNameWithoutExtension();
            LayoutContent = File.ReadAllText(fileInfo.FullName);
            ContainsPagination = LayoutContentContainsPagination(LayoutContent);
        }

        public string Id { get; private set; }
        public bool ContainsPagination { get; private set; }
        public string LayoutContent { get; private set; }

        private const string _paginatorRegExStr = @"{{ *paginator.*}}";
        private static Regex _paginatorRegEx = new Regex(_paginatorRegExStr, RegexOptions.Compiled);

        private static bool LayoutContentContainsPagination(string layoutContent)
        {
            return _paginatorRegEx.IsMatch(layoutContent);
        }
    }
}