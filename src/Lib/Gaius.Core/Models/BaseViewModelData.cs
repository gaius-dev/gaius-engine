using Gaius.Core.Worker;

namespace Gaius.Core.Models
{
    public class BaseViewModelData
    {
        public BaseViewModelData(WorkerTask workerTask, string content)
        {
            Id = workerTask.TargetId;
            Url = workerTask.TargetUrl;
            FrontMatter = workerTask.GetFrontMatter();
            Content = content;
        }

        public string Id { get; private set; }
        public string Url { get; private set; }
        public IFrontMatter FrontMatter { get; private set; }
        public string Content { get; private set; }
    }
}