using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gaius.Core.Configuration;
using Gaius.Core.Models;
using Gaius.Core.Processing.FileSystem;
using Gaius.Utilities.FileSystem;
using Gaius.Utilities.Reflection;

namespace Gaius.Core.Worker
{
    public abstract class BaseWorker : IWorker
    {
        protected static readonly GenerationInfo GenerationInfo = new GenerationInfo()
        {
            GenerationDateTime = DateTime.UtcNow,
            GaiusVersion = AssemblyUtilities.GetAssemblyVersion(AssemblyUtilities.EntryAssembly)
        };

        protected List<string> RequiredDirectories;
        protected GaiusConfiguration GaiusConfiguration;
        
        public abstract WorkerTask GenerateWorkerTask(FSInfo fsInfo);
        
        public abstract string PerformWork(WorkerTask task);
        
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

        public virtual string GetTarget(FileSystemInfo fileSystemInfo)
        {
            if (fileSystemInfo.IsDirectory())
            {
                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.SourceDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase)
                    || fileSystemInfo.FullName.Equals(GaiusConfiguration.NamedThemeDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                    return GaiusConfiguration.GenerationDirectoryName;

                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.PostsDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                    return GaiusConfiguration.PostsDirectoryName.TrimStart('_');

                return fileSystemInfo.Name;
            }

            return fileSystemInfo.Name;
        }

        public abstract WorkerMetaInfo GetWorkerMetaInfo(FileSystemInfo fileSystemInfo);

        public virtual bool GetShouldKeep(FileSystemInfo fileSystemInfo)
        {
            return GaiusConfiguration.AlwaysKeep.Any(alwaysKeep => alwaysKeep.Equals(fileSystemInfo.Name, StringComparison.InvariantCultureIgnoreCase));
        }

        protected virtual bool GetShouldSkip(FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.Name.StartsWith(".");
        }

        protected virtual bool GetIsPost(FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.IsFile() && fileSystemInfo.GetParentDirectory().Name.Equals(GaiusConfiguration.PostsDirectoryName);
        }
    }
}