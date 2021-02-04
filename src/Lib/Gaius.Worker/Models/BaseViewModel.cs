using Gaius.Worker.FrontMatter;

namespace Gaius.Worker.Models
{
    internal class BaseViewModel
    {
        internal BaseViewModel(WorkerTask workerTask, string content)
        {
            Id = workerTask.GenerationId;
            Url = workerTask.GenerationUrl;
            FrontMatter = workerTask.FrontMatter;
            Content = content;
        }

        internal string Id { get; private set; }
        internal string Url { get; private set; }
        internal IFrontMatter FrontMatter { get; private set; }
        internal string Content { get; private set; }
    }
}