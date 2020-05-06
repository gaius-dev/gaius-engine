using System;
using System.IO;

namespace Gaius.Core.Worker
{
    public abstract class BaseWorker : IWorker
    {
        private const string DOT_GIT_DIR_NAME = ".git";
        public abstract WorkerTask GenerateWorkerTask(FileSystemInfo fsInfo);
        public abstract string GetTarget(FileSystemInfo fsInfo);
        public abstract string PerformTransform(WorkerTask workerOperation);
        public virtual bool ShouldKeep(FileSystemInfo fsInfo)
        {
            if(fsInfo.Name.Equals(DOT_GIT_DIR_NAME, StringComparison.InvariantCultureIgnoreCase))
                return true;

            return false;
        }

        public virtual bool ShouldSkip(FileSystemInfo fsInfo)
        {
            if(fsInfo.Name.Equals(DOT_GIT_DIR_NAME, StringComparison.InvariantCultureIgnoreCase))
                return true;

            return false;
        }
    }
}