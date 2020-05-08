using System;
using System.Collections.Generic;
using System.IO;
using Gaius.Core.Configuration;
using Strube.Utilities.FileSystem;

namespace Gaius.Core.Worker
{
    public abstract class BaseWorker : IWorker
    {
        private const string DOT_GIT_DIR_NAME = ".git";
        protected List<string> RequiredDirectories;
        protected GaiusConfiguration GaiusConfiguration;
        public abstract WorkerTask GenerateWorkerTask(FileSystemInfo fsInfo);
        public virtual string GetTarget(FileSystemInfo fsInfo)
        {
            if (fsInfo.IsDirectory()
                && (fsInfo.FullName.Equals(GaiusConfiguration.SourceDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase)
                    || fsInfo.FullName.Equals(GaiusConfiguration.NamedThemeDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase)))
                return GaiusConfiguration.GenerationDirectoryName;

            else return fsInfo.Name;
        }

        public abstract string PerformTransform(WorkerTask workerOperation);
        public virtual bool ShouldKeep(FileSystemInfo fsInfo)
        {
            if(fsInfo.Name.Equals(DOT_GIT_DIR_NAME, StringComparison.InvariantCultureIgnoreCase))
                return true;

            return false;
        }

        public virtual bool ShouldSkip(FileSystemInfo fsInfo)
        {
            if(fsInfo.Name.StartsWith("."))
                return true;

            return false;
        }

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