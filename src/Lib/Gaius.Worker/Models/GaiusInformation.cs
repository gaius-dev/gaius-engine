using Gaius.Core.Reflection;

namespace Gaius.Worker.Models
{
    public class GaiusInformation
    {
        public GaiusInformation()
        {
            Version = AssemblyUtilities.GetAssemblyVersion(AssemblyUtilities.EntryAssembly);
        }

        public string Version { get; private set; }
    }
}