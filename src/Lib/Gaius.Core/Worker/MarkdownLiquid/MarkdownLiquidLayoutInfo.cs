using System.Collections.Generic;
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
            PaginatorIds = new List<string>();

            if(LayoutContentContainsPaginator(LayoutContent))
                PaginatorIds.AddRange(GetPaginatiatorIds(LayoutContent));
        }

        public string Id { get; private set; }
        public List<string> PaginatorIds { get; private set; }
        public bool ContainsPaginator => PaginatorIds.Count > 0;
        public string LayoutContent { get; private set; }

        private const string _paginatorRegExStr = @"{.*paginator\..*}";
        private static Regex _paginatorRegEx = new Regex(_paginatorRegExStr, RegexOptions.Compiled);

        private static bool LayoutContentContainsPaginator(string layoutContent)
        {
            return _paginatorRegEx.IsMatch(layoutContent);
        }

        private const string _paginatorIdsRegExStr = @"{.*paginator\.(?<paginator_id>[a-zA-Z0-9-_]+) .*}";
        private static Regex _paginatorIdRegEx = new Regex(_paginatorIdsRegExStr, RegexOptions.Compiled);
        private static List<string> GetPaginatiatorIds(string layoutContent)
        {
            var allPaginatorIds = new List<string>();

            MatchCollection matches = _paginatorIdRegEx.Matches(layoutContent);

            foreach (Match match in matches)
            {
                GroupCollection groups = match.Groups;
                var extractedPagId = groups["paginator_id"].Value.ToLowerInvariant();

                if(!string.IsNullOrWhiteSpace(extractedPagId) && !allPaginatorIds.Contains(extractedPagId))
                    allPaginatorIds.Add(extractedPagId);
            }

            return allPaginatorIds;
        }
    }
}