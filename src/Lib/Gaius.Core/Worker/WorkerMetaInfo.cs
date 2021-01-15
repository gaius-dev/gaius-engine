using Gaius.Core.Models;

namespace Gaius.Core.Worker
{
    public class WorkerMetaInfo
    {
        private IWorkerLayoutInfo _layoutInfo;
        private IFrontMatter _frontMatter;
        public WorkerMetaInfo(IWorkerLayoutInfo layoutInfo, IFrontMatter frontMatter, bool isPost, bool shouldSkip, bool shouldKeep)
        {
            _layoutInfo = layoutInfo;
            _frontMatter = frontMatter;
            IsPost = isPost;
            ShouldSkip = shouldSkip;
            ShouldKeep = shouldKeep;
        }

        public bool IsPost { get; private set; }
        public bool ShouldSkip { get; private set; }
        public bool ShouldKeep { get; private set; }
        public bool ShouldSkipKeep => ShouldSkip && ShouldKeep;

        //comes from IFrontMatter
        public bool IsDraft => _frontMatter?.IsDraft ?? false;
        public IFrontMatter GetFrontMatter() => _frontMatter;

        //comes from ILayoutInfo
        public string LayoutId => _layoutInfo?.Id ?? string.Empty;
        public bool ContainsPaginator => _layoutInfo?.ContainsPaginator ?? false;
        public string PaginatorId => _layoutInfo?.PaginatorId ?? string.Empty;
        public bool IsPostListing => _layoutInfo?.IsPostListing ?? false;
    }
}