using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Gaius.Core.Configuration
{
    public class GaiusArgs {
        
        private const string _automaticYes = "-y";

        public GaiusArgs(string [] args)
        {
            var listArgs = new List<string>(args);

            if (args.Contains(_automaticYes))
            {
                IsAutomaticYesEnabled = true;
                listArgs.Remove(_automaticYes);
            }

            var pathArg = ".";

            if (listArgs.Count == 0)
                IsEmptyCommand = true;

            if (listArgs.Count >= 1)
                Command = args[0];

            if (listArgs.Count >= 2)
                pathArg = args[1];

            BasePath = Path.GetFullPath(pathArg);
        }

        public string Command { get; private set; }
        public string BasePath { get; private set; }
        public bool IsAutomaticYesEnabled { get; private set; }
        public bool IsEmptyCommand { get; private set; }
        public bool IsUnknownCommand => !IsEmptyCommand && !IsVersionCommand && !IsHelpCommand && !IsShowConfigCommand && !IsTestCommand && !IsProcessCommand;
        public bool IsVersionCommand => Command == "version";
        public bool IsHelpCommand => Command == "help";
        public bool IsShowConfigCommand => Command == "showconfig";
        public bool IsTestCommand => Command == "process-test";
        public bool IsProcessCommand => Command == "process";
    }
}
