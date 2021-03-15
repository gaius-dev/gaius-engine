using System.Collections.Generic;
using System.IO;
using Gaius.Worker.Models;
using System.Linq;
using Gaius.Worker.FrontMatter;
using Gaius.Core.FileSystem;
using System;

namespace Gaius.Worker
{
    public class WorkerTask
    {
        public FileSystemInfo FileSystemInfo { get; internal set; }
        public IWorkerLayout Layout { get; internal set; }
        public IFrontMatter FrontMatter { get; internal set; }
        public bool HasFrontMatter => FrontMatter != null;
        public bool GetHasOrder(bool forSidebar) => HasFrontMatter 
                                                       && !string.IsNullOrWhiteSpace(forSidebar ? FrontMatter.SidebarOrder : FrontMatter.NavOrder);
        public DirectoryInfo DirectoryInfo => FileSystemInfo as DirectoryInfo;
        public FileInfo FileInfo => FileSystemInfo as FileInfo;
        public WorkType WorkType { get; internal set; }
        public WorkerTaskFlags TaskFlags { get; internal set; }
        public List<string> TaskPathSegments { get; internal set; }
        public List<string> TaskDirPathSegments => TaskPathSegments.Take(TaskPathSegments.Count - 1).ToList();
        public string TaskFullPath => string.Join(Path.DirectorySeparatorChar, TaskPathSegments);
        public string TaskParentDirectory => string.Join(Path.DirectorySeparatorChar, TaskPathSegments.Take(TaskPathSegments.Count - 1));
        public string TaskFileOrDirectoryName => TaskPathSegments.Last();
        public string GenerationUrl { get; internal set; }
        public string GenerationId { get; internal set; }
        public string SourceDisplay { get; internal set; }
        public string OutputDisplay { get; internal set; }
        public DateTime Date { get; internal set; }
        public string DateStr => Date.ToString("yyyy-MM-dd");

        //layout data
        public string LayoutId => Layout?.Id ?? string.Empty;

        //paginator data
        internal Paginator Paginator { get; set; }
        internal List<WorkerTask> PaginatorWorkerTasks { get; set; }
        internal bool HasPaginatorData => Paginator != null 
                                        && PaginatorWorkerTasks != null
                                        && PaginatorWorkerTasks.Count > 0;

        public bool HasGenerationFileSystemInfoMatch(FileSystemInfo genFileSystemInfo) => TaskFullPath.Equals(genFileSystemInfo.FullName);

        public bool HasGenerationFileCacheBustMatch(FileInfo genFile)
        {
            if(!genFile.IsCSSFile() && !genFile.IsJSFile())
                return false;

            var genFileNameSplit = genFile.Name.Split('-', System.StringSplitOptions.RemoveEmptyEntries);

            if(genFileNameSplit.Length <= 0)
                return false;

            var genFileNameStart = genFileNameSplit[0];

            return TaskDirPathSegments.SequenceEqual(genFile.GetDirPathSegments()) && TaskFileOrDirectoryName.StartsWith(genFileNameStart);
        }

        public bool HasGenerationParentDirectoryMatch(DirectoryInfo genDir)
        {
            return TaskParentDirectory.Contains(genDir.FullName);
        }
    }
}