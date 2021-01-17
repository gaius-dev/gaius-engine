using System.IO;
using Gaius.Core.Models;

namespace Gaius.Core.Worker
{
    public class WorkerTask
    {
        private IWorkerLayoutInfo _layoutInfo;
        private IFrontMatter _frontMatter;
        private PaginatorInfo _paginatorInfo;

        public WorkerTask(FileSystemInfo fileSystemInfo, IWorkerLayoutInfo layoutInfo, IFrontMatter frontMatter, WorkType workType, bool isPost, bool shouldSkip, bool shouldKeep, string targetFSName, string targetUrl, string targetId)
        {
            //rs: explicitly set the PaginatorInfo == null (for code understanding in the future)
            _paginatorInfo = null;

            FileSystemInfo = fileSystemInfo;
            _layoutInfo = layoutInfo;
            _frontMatter = frontMatter;
            WorkType = workType;
            IsPost = isPost;
            ShouldSkip = shouldSkip;
            ShouldKeep = shouldKeep;
            TargetFSName = targetFSName;
            TargetUrl = targetUrl;
            TargetId = targetId;
        }

        public FileSystemInfo FileSystemInfo { get; private set; }
        public DirectoryInfo DirectoryInfo => FileSystemInfo as DirectoryInfo;
        public FileInfo FileInfo => FileSystemInfo as FileInfo;
        public WorkType WorkType { get; private set; }
        public bool IsPost { get; private set; }
        public bool ShouldSkip { get; private set; }
        public bool ShouldKeep { get; private set; }
        public bool ShouldSkipKeep => ShouldSkip && ShouldKeep;
        public string TargetFSName { get; private set; }
        public string TargetUrl { get; private set; }
        public string TargetId { get; private set; }

        //comes from IFrontMatter
        public bool IsDraft => _frontMatter?.IsDraft ?? false;
        public IFrontMatter GetFrontMatter() => _frontMatter;

        //comes from ILayoutInfo
        public string LayoutId => _layoutInfo?.Id ?? string.Empty;
        public bool ContainsPaginator => _layoutInfo?.ContainsPaginator ?? false;
        public string PaginatorId => _layoutInfo?.PaginatorId ?? string.Empty;
        public bool IsPostListing => _layoutInfo?.IsPostListing ?? false;

        //PaginatorInfo get/set
        public void SetPaginatorInfo(PaginatorInfo paginatorInfo) => _paginatorInfo = paginatorInfo;
        public PaginatorInfo GetPaginatorInfo() => _paginatorInfo;
    }
}