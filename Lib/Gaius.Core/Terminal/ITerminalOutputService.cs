
using System.Collections.Generic;
using Gaius.Core.Processing.FileSystem;
using Strube.Utilities.DataStructures;

namespace Gaius.Core.Terminal
{
    public interface ITerminalOutputService
    {
        void PrintOperationTree(TreeNode<FSOperation> genOpTree);
        void PrintDefault();
        void PrintHelpCommand();
        void PrintVersionCommand();
        void PrintShowConfigurationCommand(string path);
        void PrintUnknownCommand(string command);
        void PrintMissingArgument(string argument, string command);
        void PrintSiteContainerDirectoryNotValid(List<string> validationErrors);
        void PrintInvalidConfiguration(string path);
    }
}