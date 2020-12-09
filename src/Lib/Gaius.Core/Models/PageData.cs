using Gaius.Core.Worker;

namespace Gaius.Core.Models
{
    public class PageData
    {
        public PageData(IFrontMatter frontMatter, string html, WorkerTask task)
        {
            FrontMatter = frontMatter;
            Html = html;
            Id = task.TargetId;
            Url = task.TargetUrl;
        }

        public IFrontMatter FrontMatter { get; private set;}
        public string Html { get; private set; }
        public string Id { get; private set; }
        public string Url { get; private set; }
    }
}