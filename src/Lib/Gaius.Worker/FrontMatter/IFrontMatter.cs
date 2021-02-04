using System.Collections.Generic;
using Gaius.Worker.Models;

namespace Gaius.Worker.FrontMatter
{
    public interface IFrontMatter
    {
        string Layout { get; }
        string Title { get; }
        string Author { get; }
        string Keywords { get; }
        string Description { get; }
        string Tag { get; }
        List<string> Tags { get; }
        List<TagData> GetTagData();
    }
}