using System.IO;
using Gaius.Core.Models;

namespace Gaius.Core.Parsing
{
    public interface IFrontMatterParser
    {
        IFrontMatter GetFrontMatter(FileSystemInfo fileSystemInfo);
    }
}