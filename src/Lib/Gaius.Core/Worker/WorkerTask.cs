using System.Collections.Generic;
using System.IO;
using Gaius.Core.Models;

namespace Gaius.Core.Worker
{
    public class WorkerTask
    {
        private IWorkerLayoutData _layoutData;
        private IFrontMatter _frontMatter;
        private PaginatorData _paginatorInfo;
        private List<WorkerTask> _paginatorWorkerTasks;

        public WorkerTask(FileSystemInfo fileSystemInfo, IWorkerLayoutData layoutData, IFrontMatter frontMatter, WorkType workType, bool isPost, bool shouldSkip, bool shouldKeep, string targetFSName, string targetUrl, string targetId)
        {
            _paginatorInfo = null;
            _paginatorWorkerTasks = null;

            FileSystemInfo = fileSystemInfo;
            _layoutData = layoutData;
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

        //comes from IWorkerLayoutData
        public string LayoutId => _layoutData?.Id ?? string.Empty;
        public string PaginatorId => _layoutData?.PaginatorId ?? string.Empty;
        public bool IsListing => _layoutData?.IsListing ?? false;
        public bool IsDefaultPostListing => _layoutData?.IsDefaultPostListing ?? false;

        //PaginatorData get/set
        public void AddPaginatorData(PaginatorData paginatorInfo) => _paginatorInfo = paginatorInfo;
        public PaginatorData GetPaginatorData() => _paginatorInfo;
        public bool HasPaginatorData => _paginatorInfo != null;

        //PaginatorWorkerTasks get/set
        public void AddPaginatorWorkerTasks(List<WorkerTask> paginatorWorkerTasks) => _paginatorWorkerTasks = paginatorWorkerTasks;
        public List<WorkerTask> GetPaginatorWorkerTasks() => _paginatorWorkerTasks;
        public bool HasPaginatorWorkerTasks => _paginatorWorkerTasks != null;
    }
}