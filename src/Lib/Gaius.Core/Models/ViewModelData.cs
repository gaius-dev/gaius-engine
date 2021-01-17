using Gaius.Core.Worker;

namespace Gaius.Core.Models
{
    public class ViewModelData
    {
        public ViewModelData(WorkerTask workerTask, GenerationInfo generationInfo, string html)
        {
            Id = workerTask.TargetId;
            Url = workerTask.TargetUrl;
            FrontMatter = workerTask.GetFrontMatter();
            GenerationInfo = generationInfo;
            Html = html;
        }

        public string Id { get; private set; }
        public string Url { get; private set; }
        public IFrontMatter FrontMatter { get; private set; }
        public GenerationInfo GenerationInfo { get; private set; }
        public string Html { get; private set; }
    }
}