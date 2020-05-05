using System.IO;

namespace Gaius.Core.Worker
{
    public interface IWorker
    {
        WorkerTask GenerateWorkerTask(FileSystemInfo fsInfo);
        string PerformTransform(WorkerTask workerOperation);
        string GetTarget(FileSystemInfo fsInfo);
    }
}