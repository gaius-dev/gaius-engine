using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using Gaius.Utilities.FileSystem;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidLayoutData : IWorkerLayoutData
    {
        public MarkdownLiquidLayoutData(FileInfo fileInfo)
        {
            Id = fileInfo.GetNameWithoutExtension();
            LayoutContent = File.ReadAllText(fileInfo.FullName);

            if(LayoutContentContainsPaginator(LayoutContent))
                PaginatorId = GetPaginatiatorIdForLayout(Id, LayoutContent);
        }

        public string Id { get; private set; }
        public string PaginatorId { get; private set; }
        public bool IsListing => !string.IsNullOrWhiteSpace(PaginatorId);
        public bool IsDefaultPostListing => IsListing && PaginatorId.Equals("posts");
        public string LayoutContent { get; private set; }

        private const string _paginatorRegExStr = @"{.*paginator\..*}";
        private static Regex _paginatorRegEx = new Regex(_paginatorRegExStr, RegexOptions.Compiled);

        private static bool LayoutContentContainsPaginator(string layoutContent)
        {
            return _paginatorRegEx.IsMatch(layoutContent);
        }

        private const string _paginatorIdsRegExStr = @"{.*paginator\.(?<paginator_id>[a-zA-Z0-9-_]+) .*}";
        private static Regex _paginatorIdRegEx = new Regex(_paginatorIdsRegExStr, RegexOptions.Compiled);
        private static string GetPaginatiatorIdForLayout(string layoutId, string layoutContent)
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

            if (allPaginatorIds.Count > 1)
                throw new Exception($"Layout '{layoutId}' contains paginator(s) with more than one ID: '{string.Join(',', allPaginatorIds)}'");

            return allPaginatorIds.FirstOrDefault();
        }
    }
}