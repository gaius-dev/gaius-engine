using System.Collections.Generic;
using System.IO;
using Gaius.Core.Models;

namespace Gaius.Core.Worker
{
    public interface IWorker
    {
        WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo);
        void AddPaginatorDataToWorkerTask(WorkerTask workerTask, PaginatorData paginatorData, List<WorkerTask> paginatorWorkerTasks);
        WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo, PaginatorData paginatorData, List<WorkerTask> paginatorWorkerTasks);
        string PerformWork(WorkerTask task);
        (bool, List<string>) ValidateSiteContainerDirectory();
        string GetTarget(FileSystemInfo fileSystemInfo, int page = 1);
        bool GetShouldKeep(FileSystemInfo fileSystemInfo);
    }
}