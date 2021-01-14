using Gaius.Core.Models;

namespace Gaius.Core.Parsing
{
    public interface IFrontMatterParser
    {
        IFrontMatter DeserializeFromContent(string textContent);
    }
}