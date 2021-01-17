using System.Collections.Generic;
using System.IO;

namespace Gaius.Core.Worker
{
    public interface IWorker
    {
        WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo);
        string PerformWork(WorkerTask task);
        (bool, List<string>) ValidateSiteContainerDirectory();

        string GetTarget(FileSystemInfo fileSystemInfo);
        bool GetShouldKeep(FileSystemInfo fileSystemInfo);
    }
}