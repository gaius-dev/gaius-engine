using System.IO;

namespace Gaius.Core.Worker
{
    public class WorkerTask
    {
        public FileSystemInfo FSInfo { get; private set;}
        public string Target { get; private set;}
        public WorkType TransformType { get; private set;}

        public WorkerTask(FileSystemInfo fsInput, WorkType transformType, string target)
        {
            FSInfo = fsInput;
            TransformType = transformType;
            Target = target;
        }
    }
}