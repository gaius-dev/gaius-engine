using Gaius.Core.Models;

namespace Gaius.Core.Worker
{
    public class WorkerFSMetaInfo
    {
        public IFrontMatter FrontMatter { get; set; }
        public bool IsPost { get; set; }
        public bool ShouldSkip { get; set; }
        public bool ShouldKeep { get; set; }
        public bool ContainsPagination { get; set; }
    }
}