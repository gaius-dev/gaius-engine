using System.Collections.Generic;

namespace Gaius.Worker.FrontMatter
{
    public interface IFrontMatter
    {
        string Layout { get; }
        string Title { get; }
        string Author { get; }
        string AuthorPage { get; }
        string AuthorImage { get; }
        string Keywords { get; }
        string Description { get; }
        string Image { get; }
        List<string> Tags { get; }
        List<string> GetTags();
    }
}