using System.Collections.Generic;
using System.IO;
using Gaius.Worker.Models;

namespace Gaius.Worker
{
    public interface IWorker
    {
        void InitWorker();
        WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo);
        void AddNavDataToWorker(List<BaseNavData> navData);
        void AddSidebarDataToWorker(List<BaseNavData> sidebarData);
        void AddTagDataToWorker(List<TagData> tagData, bool tagListPageExists);
        WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo, Paginator paginator, List<WorkerTask> paginatorWorkerTasks);
        string PerformWork(WorkerTask task);
        (bool, List<string>) ValidateSiteContainerDirectory();
    }
}