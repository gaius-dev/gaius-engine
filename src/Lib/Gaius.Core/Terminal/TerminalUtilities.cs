using System;
using System.Collections.Generic;
using System.Drawing;
using Gaius.Core.Colors;
using Gaius.Core.Configuration;
using Gaius.Core.Reflection;
using Newtonsoft.Json;

namespace Gaius.Core.Terminal
{
    public class TerminalUtilities
    {
        public static readonly Color _colorBlue = ConsoleColor.Blue.ToColor();
        public static readonly Color _colorGreen = ConsoleColor.DarkGreen.ToColor();
        public static readonly Color _colorRed = ConsoleColor.DarkRed.ToColor();
        public static readonly Color _colorYellow = ConsoleColor.Yellow.ToColor();
        public static readonly Color _colorMagenta = ConsoleColor.DarkMagenta.ToColor();
        public static readonly Color _colorCyan = ConsoleColor.DarkCyan.ToColor();
        public static readonly Color _colorWhite = ConsoleColor.White.ToColor();
        public static readonly Color _colorGrey = ConsoleColor.Gray.ToColor();
        public static readonly Color _colorFile = _colorWhite;
        public static readonly Color _colorDirectory = _colorBlue;
        public static readonly Color _colorFileMarkdown = _colorYellow;
        public static readonly Color _colorFileHtml = _colorGreen;
        public static readonly Color _colorFileLiquid = _colorGrey;
        public static readonly Color _colorFileJavascript = _colorCyan;
        public static readonly Color _colorFileCssLess = _colorMagenta;
        
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

        public static void PrintDefault()
        {
            PrintStylizedApplicationName();
            PrintApplicationNameAndVersion();
            PrintUsage();
        }

        public static void PrintHelpCommand()
        {
            PrintUsage();
        }

        public static void PrintVersionCommand()
        {
            PrintStylizedApplicationName();
            PrintApplicationNameAndVersion();
        }

        public static void PrintShowConfigurationCommand(string path, GaiusConfiguration gaiusConfiguration)
        {
            Console.WriteLine();
            PrintConfiguration(path, gaiusConfiguration, _colorWhite);
        }

        public static void PrintUnknownCommand(string command)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine($"Unrecognized command or argument '{command}'.", _colorRed);
            PrintUsage();
        }

        public static void PrintMissingArgument(string argument, string command)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine($"Missing '{argument}' argument for the '{command}' command.", _colorRed);
            PrintUsage();
        }

        private static void PrintStylizedApplicationName()
        {
            var stylizedApplicationName =
@"
              _           
   __ _  __ _(_)_   _ ___ 
  / _` |/ _` | | | | / __|
 | (_| | (_| | | |_| \__ \
  \__, |\__,_|_|\__,_|___/
  |___/                   
";
            Colorful.Console.WriteLine(stylizedApplicationName, Color.Gold);
        }
        
        public static void PrintSiteContainerDirectoryNotValid(List<string> validationErrors, GaiusConfiguration gaiusConfiguration)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine($"The site container directory '{gaiusConfiguration.SiteContainerFullPath}' is not valid.", _colorRed);
            System.Console.WriteLine();
            Colorful.Console.WriteLine("Validation errors:", _colorRed);

            for(var i = 0; i < validationErrors.Count; i++)
            {
                Colorful.Console.WriteLine($"{i+1}. {validationErrors[i]}", _colorRed);
            }

            System.Console.WriteLine();
        }

        private static void PrintConfiguration(string path, GaiusConfiguration gaiusConfiguration, Color jsonColor)
        {
            Console.WriteLine($"Using configuration values defined in: {path}/gaius.json");
            Colorful.Console.WriteLine($"Note: if no configuration file is present, default configuration values are used.", _colorGrey);
            Console.WriteLine();
            Colorful.Console.WriteLine(JsonConvert.SerializeObject(gaiusConfiguration, Formatting.Indented), jsonColor);
            System.Console.WriteLine();
        }

        private static void PrintUsage()
        {
            var usage = 
@"
Usage: gaius [command] [options]

Commands (Gaius Engine):

  version               Show version information.
  help                  Show help information.
  showconfig [path]     Show the configuration in [path].
  build [path]          Build site based the source data in [path] using the config file [path]/gaius.json.
  serve [path]          Build and serve site based the source data in [path] using the config file [path]/gaius.json.
                            Note: testmode is enabled automatically.
Options:

  --yes                 Automatically answer 'yes' for all questions.
                            This allows for automatic processing of source data.

  --testmode            When building site, Gaius will *not* prepend the 'GenerationUrlRootPrefix' config param to URLs.
                            Note: testmode is enabled automatically when serving your site.
                            This allows for local testing of generated sites (e.g. http://localhost).

";
            System.Console.WriteLine(usage);
        }

        public static void PrintInvalidConfiguration()
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine("Invalid gaius.json configuration file.", _colorRed);
        }
    }
}