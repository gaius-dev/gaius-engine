using System;

namespace Gaius.Worker.Models
{
    public class NavData : BaseNavData
    {
        public NavData(WorkerTask workerTask)
        {
            if(!workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                throw new ArgumentException($"{nameof(workerTask.TaskFlags)} must contain {nameof(WorkerTaskFlags.IsChildOfSourceDir)}");

            Id = workerTask.GenerationId;
            Title =  workerTask.FrontMatter?.NavTitle ?? workerTask.FrontMatter?.Title ?? workerTask.TaskFileOrDirectoryName;
            Url = workerTask.GenerationUrl;
            Order = workerTask.FrontMatter?.NavOrder;
            Level = workerTask.FrontMatter?.NavLevel ?? -1;
        }
    }
}