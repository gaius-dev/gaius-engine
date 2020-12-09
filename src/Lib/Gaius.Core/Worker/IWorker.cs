using System.Collections.Generic;
using System.IO;

namespace Gaius.Core.Worker
{
    public interface IWorker
    {
        WorkerTask GenerateWorkerTask(FileSystemInfo fsInfo);
        string PerformWork(WorkerTask task);
        string GetTarget(FileSystemInfo fsInfo);
        bool ShouldKeep(FileSystemInfo fsInfo);
        bool ShouldSkip(FileSystemInfo fsInfo);        
        bool IsDraft(FileSystemInfo fsInfo);
        (bool, List<string>) ValidateSiteContainerDirectory();
    }
}