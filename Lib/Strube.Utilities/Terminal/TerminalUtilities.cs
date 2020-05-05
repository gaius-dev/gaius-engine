using System;
using System.Drawing;
using Strube.Utilities.Reflection;

namespace Strube.Utilities.Terminal
{
    public class TerminalUtilities
    {
        /*
        private static Stream GetEmbeddedFlfFromAssembly()
        {
            return AssemblyUtilities.GetEmbeddedAssemblyResource(System.Reflection.Assembly.GetExecutingAssembly(), "Gaius.Utilities.EmbeddedResources.speed.flf");
        }
        */

        public static void EnterToExit()
        {
            System.Console.WriteLine();
            System.Console.WriteLine("Press enter to exit.");
            System.Console.ReadLine();
        }

        public static bool YesToContinue()
        {
            System.Console.WriteLine("Do you wish to continue? (y/n)");
            var answer = System.Console.ReadLine();
            return answer.Equals("y", StringComparison.CurrentCultureIgnoreCase);
        }

        public static void PrintApplicationNameAndVersion()
        {
            var assemblyTitle = AssemblyUtilities.GetAssemblyTitle(AssemblyUtilities.EntryAssembly);
            var assemblyVersion = AssemblyUtilities.GetAssemblyVersion(AssemblyUtilities.EntryAssembly);
            System.Console.WriteLine($"{assemblyTitle} {assemblyVersion}");
        }

        public static void PrintBasePathDoesNotExist(string basePath)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine($"The specified path '{basePath}' does not exist.", Color.DarkRed);
        }
    }
}