using System.IO;
using System.Linq;
using System.Reflection;

namespace Gaius.Utilities.Reflection
{
    public static class AssemblyUtilities
    {
        public static Assembly EntryAssembly => System.Reflection.Assembly.GetEntryAssembly();

        public static string GetAssemblyTitle(Assembly assembly)
        {
            var assemblyAttribute = 
                assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute)).FirstOrDefault();

            return assemblyAttribute != null ? (assemblyAttribute as AssemblyTitleAttribute).Title : string.Empty;
        }

        public static string GetAssemblyName(Assembly assembly)
        {
            return assembly.GetName().Name;
        }

        public static string GetAssemblyVersion(Assembly assembly)
        {
            return assembly.GetName().Version.ToString(3);
        }

        public static Stream GetEmbeddedAssemblyResource(Assembly assembly, string embeddedResourceName)
        {
            return assembly.GetManifestResourceStream(embeddedResourceName);
        }
    }
}