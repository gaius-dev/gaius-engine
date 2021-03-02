using System;

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
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Url { get; private set; }
        public string Order { get; private set; }
        public int Level => GetLevelFromOrder(Order);

        internal static int GetLevelFromOrder(string order)
        {
            if(string.IsNullOrWhiteSpace(order))
                    return -1;

            return order.Split('.', StringSplitOptions.RemoveEmptyEntries).Length - 1;
        }
    }
}