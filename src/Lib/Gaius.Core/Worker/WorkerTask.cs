using System.Collections.Generic;
using System.IO;
using Gaius.Core.Models;
using System.Linq;

namespace Gaius.Core.Worker
{
    public class WorkerTask
    {
        public FileSystemInfo FileSystemInfo { get; internal set; }
        public IWorkerLayout Layout { get; internal set; }
        public IFrontMatter FrontMatter { get; internal set; }
        public DirectoryInfo DirectoryInfo => FileSystemInfo as DirectoryInfo;
        public FileInfo FileInfo => FileSystemInfo as FileInfo;
        public WorkType WorkType { get; internal set; }
        public bool IsPost { get; internal set; }
        public bool IsDraft { get; internal set; }
        public bool IsSkip { get; internal set; }
        public bool IsKeep { get; internal set; }
        public List<string> TargetPathSegments { get; internal set; }
        public string TargetFullPath => string.Join(Path.DirectorySeparatorChar, TargetPathSegments);
        public string TargetParentDirectory => string.Join(Path.DirectorySeparatorChar, TargetPathSegments.Take(TargetPathSegments.Count - 1));
        public string TargetFileOrDirectoryName => TargetPathSegments.Last();
        public string TargetUrl { get; internal set; }
        public string TargetId { get; internal set; }
        public string YearStr { get; internal set; }
        public string MonthStr { get; internal set; }
        public string DayStr { get; internal set; }
        public string SourceDisplayName { get; internal set; }
        public string TargetDisplayName { get; internal set; }

        //comes from IWorkerLayoutData
        public string LayoutId => Layout?.Id ?? string.Empty;
        public string PaginatorId => Layout?.PaginatorId ?? string.Empty;
        public bool IsPostListing => Layout?.IsPostListing ?? false;

        //paginator data
        internal Paginator Paginator { get; set; }
        internal List<WorkerTask> PaginatorWorkerTasks { get; set; }
        internal bool HasPaginatorData => Paginator != null 
                                        && PaginatorWorkerTasks != null
                                        && PaginatorWorkerTasks.Count > 0;
    }
}