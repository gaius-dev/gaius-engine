using System.IO;
using Gaius.Core.Configuration;
using Gaius.Utilities.FileSystem;
using System.Linq;
using System;

namespace Gaius.Core.Worker
{
    public class WorkerTask
    {
        public FileSystemInfo FSInfo { get; private set; }
        public WorkType WorkType { get; private set; }
        public string TargetFSName { get; private set; }
        public string TargetUrl { get; private set; }
        public string TargetId { get; private set;}

        public WorkerTask(FileSystemInfo fsInput, WorkType workType, string targetFSName, GaiusConfiguration gaiusConfiguration)
        {
            FSInfo = fsInput;
            WorkType = workType;
            TargetFSName = targetFSName;

            if(fsInput.IsDirectory())
                return;

            var pathSegments = fsInput.GetPathSegments();

            var sourceDirIndex = Array.IndexOf(pathSegments, gaiusConfiguration.SourceDirectoryName);
            var skipAmt = sourceDirIndex + 1;

            if(skipAmt > pathSegments.Length)
                return;

            var pathSegmentsToKeep = pathSegments.Skip(skipAmt).ToList();

            if(pathSegmentsToKeep.Count == 0)
                return;
            
            pathSegmentsToKeep[pathSegmentsToKeep.Count - 1] = targetFSName;
            TargetUrl = $"{gaiusConfiguration.GetGenerationUrlRootPrefix()}/{string.Join("/", pathSegmentsToKeep)}";

            var targetWithoutExt = Path.GetFileNameWithoutExtension(targetFSName);
            pathSegmentsToKeep[pathSegmentsToKeep.Count - 1] = targetWithoutExt;
            TargetId = $"{string.Join(".", pathSegmentsToKeep)}";
        }
    }
}