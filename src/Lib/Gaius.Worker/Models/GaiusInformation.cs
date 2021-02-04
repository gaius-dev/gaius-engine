using Gaius.Core.Reflection;

namespace Gaius.Worker.Models
{
    internal class GaiusInformation
    {
        public GaiusInformation()
        {
            Version = AssemblyUtilities.GetAssemblyVersion(AssemblyUtilities.EntryAssembly);
        }

        internal string Version { get; private set; }
    }
}