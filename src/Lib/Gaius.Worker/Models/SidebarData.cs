using System;

namespace Gaius.Worker.Models
{
    public class SidebarData : BaseNavData
    {
        public SidebarData(WorkerTask workerTask)
        {
            if(!workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                throw new ArgumentException($"{nameof(workerTask.TaskFlags)} must contain {nameof(WorkerTaskFlags.IsChildOfSourceDir)}");

            Id = workerTask.GenerationId;
            Title =  workerTask.FrontMatter?.SidebarTitle ?? workerTask.FrontMatter?.NavTitle ?? workerTask.FrontMatter?.Title ?? workerTask.TaskFileOrDirectoryName;
            Url = workerTask.GenerationUrl;
            Order = workerTask.FrontMatter?.SidebarOrder;
            Level = workerTask.FrontMatter?.SidebarLevel ?? -1;
        }
    }
}