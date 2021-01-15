using System.Collections.Generic;
using Gaius.Core.Models;

namespace Gaius.Core.Worker
{
    public class WorkerFSMetaInfo
    {
        public IFrontMatter FrontMatter { get; set; }
        public bool IsPost { get; set; }
        public bool ShouldSkip { get; set; }
        public bool ShouldKeep { get; set; }
        public List<string> PaginatorIds { get; set; }
        public bool ContainsPaginator => PaginatorIds.Count > 0;
    }
}