using Gaius.Worker.Models;

namespace Gaius.Worker.Parsing
{
    public interface IFrontMatterParser
    {
        IFrontMatter DeserializeFromContent(string textContent);
    }
}