using System.Collections.Generic;
using System.IO;
using Gaius.Worker.Models;
using System.Linq;
using Gaius.Worker.FrontMatter;

namespace Gaius.Worker
{
    public class WorkerTask
    {
        public FileSystemInfo FileSystemInfo { get; internal set; }
        public IWorkerLayout Layout { get; internal set; }
        public IFrontMatter FrontMatter { get; internal set; }
        public bool HasFrontMatter => FrontMatter != null;
        public DirectoryInfo DirectoryInfo => FileSystemInfo as DirectoryInfo;
        public FileInfo FileInfo => FileSystemInfo as FileInfo;
        public WorkType WorkType { get; internal set; }
        public WorkerTaskFlags TaskFlags { get; internal set; }
        public List<string> TaskPathSegments { get; internal set; }
        public string TaskFullPath => string.Join(Path.DirectorySeparatorChar, TaskPathSegments);
        public string TaskParentDirectory => string.Join(Path.DirectorySeparatorChar, TaskPathSegments.Take(TaskPathSegments.Count - 1));
        public string TaskFileOrDirectoryName => TaskPathSegments.Last();
        public string GenerationUrl { get; internal set; }
        public string GenerationId { get; internal set; }
        public string SourceDisplay { get; internal set; }
        public string OutputDisplay { get; internal set; }

        //layout data
        public string LayoutId => Layout?.Id ?? string.Empty;
        public bool IsPostListing => Layout?.IsPostListing ?? false;

        //paginator data
        internal Paginator Paginator { get; set; }
        internal List<WorkerTask> PaginatorWorkerTasks { get; set; }
        internal bool HasPaginatorData => Paginator != null 
                                        && PaginatorWorkerTasks != null
                                        && PaginatorWorkerTasks.Count > 0;
    }
}