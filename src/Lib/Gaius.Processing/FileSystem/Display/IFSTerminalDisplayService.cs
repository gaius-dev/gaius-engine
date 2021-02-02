
using System.Collections.Generic;
using Gaius.Core.DataStructures;

namespace Gaius.Processing.FileSystem.Display
{
    public interface IFSTerminalDisplayService
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