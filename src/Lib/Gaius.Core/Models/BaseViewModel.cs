using Gaius.Core.Worker;

namespace Gaius.Core.Models
{
    public class BaseViewModel
    {
        public BaseViewModel(WorkerTask workerTask, string content)
        {
            Id = workerTask.GenerationId;
            Url = workerTask.GenerationUrl;
            FrontMatter = workerTask.FrontMatter;
            Content = content;
        }

        public string Id { get; private set; }
        public string Url { get; private set; }
        public IFrontMatter FrontMatter { get; private set; }
        public string Content { get; private set; }
    }
}