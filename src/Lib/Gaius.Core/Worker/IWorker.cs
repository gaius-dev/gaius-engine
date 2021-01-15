using System.Collections.Generic;
using System.IO;
using Gaius.Core.Models;
using Gaius.Core.Processing.FileSystem;

namespace Gaius.Core.Worker
{
    public interface IWorker
    {
        WorkerTask GenerateWorkerTask(FSInfo fsInfo);
        string PerformWork(WorkerTask task);
        (bool, List<string>) ValidateSiteContainerDirectory();

        string GetTarget(FileSystemInfo fileSystemInfo);
        WorkerMetaInfo GetWorkerMetaInfo(FileSystemInfo fileSystemInfo);
        bool GetShouldKeep(FileSystemInfo fileSystemInfo);
    }
}