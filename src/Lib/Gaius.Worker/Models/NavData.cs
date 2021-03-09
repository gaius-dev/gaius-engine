using System;
using System.Collections.Generic;

namespace Gaius.Worker.Models
{
    public class NavData 
    {
        public NavData(WorkerTask workerTask)
        {
            if(!workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                throw new ArgumentException($"{nameof(workerTask.TaskFlags)} must contain {nameof(WorkerTaskFlags.IsChildOfSourceDir)}");

            Id = workerTask.GenerationId;
            Title = workerTask.FrontMatter?.NavTitle ?? workerTask.FrontMatter?.Title ?? workerTask.TaskFileOrDirectoryName;
            Url = workerTask.GenerationUrl;
            Order = workerTask.FrontMatter?.NavOrder;
            Level = workerTask.FrontMatter?.NavLevel ?? -1;
            InHeader = workerTask.FrontMatter?.NavInHeader ?? false;
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Url { get; private set; }
        public string Order { get; private set; }
        public int Level { get; private set; }
        public bool InHeader { get; private set; }
        public List<NavData> Children { get; private set; }

        public void AddChildNavData(List<NavData> childNavData)
        {
            Children = childNavData;
        }
    }
}