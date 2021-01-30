using System.Collections.Generic;
using System.IO;
using Gaius.Core.Models;

namespace Gaius.Core.Worker
{
    public interface IWorker
    {
        WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo);
        void AddPaginatorDataToWorkerTask(WorkerTask workerTask, Paginator paginator, List<WorkerTask> paginatorWorkerTasks);
        WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo, Paginator paginator, List<WorkerTask> paginatorWorkerTasks);
        string PerformWork(WorkerTask task);
        (bool, List<string>) ValidateSiteContainerDirectory();
    }
}