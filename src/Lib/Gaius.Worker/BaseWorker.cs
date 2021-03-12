using System.Collections.Generic;
using System.IO;
using Gaius.Core.Configuration;
using Gaius.Worker.Models;

namespace Gaius.Worker
{
    public abstract class BaseWorker : IWorker
    {
        protected List<string> RequiredDirectories;
        protected GaiusConfiguration GaiusConfiguration;
        internal SiteData SiteData;
        internal static readonly GaiusInformation GaiusInformation = new GaiusInformation();

        public abstract void InitWorker();
        public abstract WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo);
        public abstract void AddNavDataToWorker(List<BaseNavData> navData);
        public abstract void AddSidebarDataToWorker(List<BaseNavData> sidebarData);
        public abstract void AddTagDataToWorker(List<TagData> tagData, bool tagListPageExists);
        public abstract WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo, Paginator paginator, List<WorkerTask> paginatorWorkerTasks);
        public abstract string PerformWork(WorkerTask workerTask);
        public (bool, List<string>) ValidateSiteContainerDirectory()
        {
            var validationErrors = new List<string>();

            var sourceDirExists = Directory.Exists(GaiusConfiguration.SourceDirectoryFullPath);

            if (!sourceDirExists)
                validationErrors.Add($"The source directory '{GaiusConfiguration.SourceDirectoryFullPath}' does not exist.");

            var themesDirExists = Directory.Exists(GaiusConfiguration.NamedThemeDirectoryFullPath);

            if (!themesDirExists)
                validationErrors.Add($"The themes directory '{GaiusConfiguration.NamedThemeDirectoryFullPath}' does not exist.");

            var otherReqDirsExist = true;

            foreach(var reqDirFullPath in RequiredDirectories)
            {
                if(!Directory.Exists(reqDirFullPath))
                {
                    otherReqDirsExist = false;
                    validationErrors.Add($"The required directory '{reqDirFullPath}' does not exist.");
                }
            }

            return (sourceDirExists && otherReqDirsExist, validationErrors);
        }
    }
}