using System;
using System.Collections.Generic;
using System.IO;
using Gaius.Core.Configuration;
using Gaius.Worker.Models;
using Gaius.Core.Reflection;

namespace Gaius.Worker
{
    public abstract class BaseWorker : IWorker
    {
        protected List<string> RequiredDirectories;
        protected GaiusConfiguration GaiusConfiguration;
        protected static readonly GenerationInfo GenerationInfo = new GenerationInfo()
        {
            GenerationDateTime = DateTime.UtcNow,
            GaiusVersion = AssemblyUtilities.GetAssemblyVersion(AssemblyUtilities.EntryAssembly)
        };

        public abstract WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo);
        public abstract void AddPaginatorDataToWorkerTask(WorkerTask workerTask, Paginator paginator, List<WorkerTask> paginatorWorkerTasks);
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