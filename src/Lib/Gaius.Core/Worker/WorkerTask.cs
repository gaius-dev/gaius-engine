using System.IO;
using Gaius.Core.Configuration;
using Gaius.Utilities.FileSystem;
using System.Linq;
using System;
using Gaius.Core.Processing.FileSystem;

namespace Gaius.Core.Worker
{
    public class WorkerTask
    {
        public FSInfo FSInfo { get; private set; }
        public WorkType WorkType { get; private set; }
        public string TargetFSName { get; private set; }
        public string TargetUrl { get; private set; }
        public string TargetId { get; private set;}

        public WorkerTask(FSInfo fsInput, WorkType workType, string targetFSName, GaiusConfiguration gaiusConfiguration)
        {
            FSInfo = fsInput;
            WorkType = workType;
            TargetFSName = targetFSName;

            if(fsInput.FileSystemInfo.IsDirectory())
                return;

            var pathSegments = fsInput.FileSystemInfo.GetPathSegments();

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