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

        public bool ShouldKeep(FileSystemInfo fsInfo)
        {
            if(GaiusConfiguration.AlwaysKeep.Any(alwaysKeep => alwaysKeep.Equals(fsInfo.Name, StringComparison.InvariantCultureIgnoreCase)))
                return true;

            return false;
        }

        public virtual string GetTarget(FileSystemInfo fsInfo)
        {
            if (fsInfo.IsDirectory())
            {
                if(fsInfo.FullName.Equals(GaiusConfiguration.SourceDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase)
                    || fsInfo.FullName.Equals(GaiusConfiguration.NamedThemeDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                    return GaiusConfiguration.GenerationDirectoryName;

                if(fsInfo.FullName.Equals(GaiusConfiguration.PostsDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                    return GaiusConfiguration.PostsDirectoryName.TrimStart('_');

                return fsInfo.Name;
            }

            else return fsInfo.Name;
        }

        public abstract WorkerFSMetaInfo GetWorkerFSMetaInfo(FileSystemInfo fileSystemInfo);
        
        protected bool IsPost(FileSystemInfo fileSystemInfo)
        {
            if(fileSystemInfo.IsFile() && fileSystemInfo.GetParentDirectory().Name.Equals(GaiusConfiguration.PostsDirectoryName))
                return true;

            return false;
        }

        protected virtual bool ShouldSkip(FileSystemInfo fsInfo)
        {
            if(fsInfo.Name.StartsWith("."))
                return true;

            return false;
        }
    }
}