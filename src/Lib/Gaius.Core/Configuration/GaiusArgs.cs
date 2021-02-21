using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Gaius.Core.Configuration
{
    public class GaiusArgs {
        
        private const string _automaticYesSwitch = "--yes";
        private const string _testModeSwitch = "--testmode";
        private bool _noCommand;
        private bool _testMode;

        public GaiusArgs(string [] args)
        {
            var listArgs = new List<string>(args);

            if (args.Contains(_automaticYesSwitch))
            {
                IsAutomaticYesEnabled = true;
                listArgs.Remove(_automaticYesSwitch);
            }

            if (args.Contains(_testModeSwitch))
            {
                _testMode = true;
                listArgs.Remove(_testModeSwitch);
            }

            var pathArg = ".";

            if (listArgs.Count == 0)
                _noCommand = true;

            if (listArgs.Count >= 1)
                Command = args[0];

            if (listArgs.Count >= 2)
                pathArg = args[1];

            BasePath = Path.GetFullPath(pathArg);
        }

        public string Command { get; private set; }
        public string BasePath { get; private set; }
        public bool IsAutomaticYesEnabled { get; private set; }
        public bool IsTestModeEnabled => IsServeCommand || _testMode;
        public bool IsUnknownCommand => !IsVersionCommand && !IsHelpCommand && !IsShowConfigCommand && !IsBuildCommand && !IsServeCommand;
        public bool IsVersionCommand => Command == "version";
        public bool IsHelpCommand => Command == "help";
        public bool IsShowConfigCommand => Command == "showconfig";
        public bool IsBuildCommand => Command == "build";
        public bool IsServeCommand => Command == "serve";
        public bool NoCommand => _noCommand;
    }
}
