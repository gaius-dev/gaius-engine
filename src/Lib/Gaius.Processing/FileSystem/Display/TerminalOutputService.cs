using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Gaius.Core.Configuration;
using Gaius.Core.Colors;
using Gaius.Core.DataStructures;
using Gaius.Core.Terminal;
using Newtonsoft.Json;
using Gaius.Worker;
using Gaius.Core.FileSystem;

namespace Gaius.Processing.FileSystem.Display
{
    public class FSTerminalDisplayService : IFSTerminalDisplayService
    {
        private const string INDENT = "|_ ";
        private static readonly Color BLUE_COLOR = ConsoleColor.Blue.ToColor();
        private static readonly Color GREEN_COLOR = ConsoleColor.DarkGreen.ToColor();
        private static readonly Color RED_COLOR = ConsoleColor.DarkRed.ToColor();
        private static readonly Color YELLOW_COLOR = ConsoleColor.Yellow.ToColor();
        private static readonly Color MAGENTA_COLOR = ConsoleColor.DarkMagenta.ToColor();
        private static readonly Color CYAN_COLOR = ConsoleColor.DarkCyan.ToColor();
        private static readonly Color WHITE_COLOR = ConsoleColor.White.ToColor();
        private static readonly Color GREY_COLOR = ConsoleColor.Gray.ToColor();
        private static readonly Color MARKDOWN_COLOR = YELLOW_COLOR;
        private static readonly Color FILE_COLOR = WHITE_COLOR;
        private static readonly Color DIRECTORY_COLOR = BLUE_COLOR;
        private static readonly Color HTML_COLOR = GREEN_COLOR;
        private static readonly Color LIQUID_COLOR = GREY_COLOR;
        private static readonly Color JS_COLOR = CYAN_COLOR;
        private static readonly Color CSS_LESS_SASS_COLOR = MAGENTA_COLOR;
        
        private GaiusConfiguration _gaiusConfiguration;

        public FSTerminalDisplayService(IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            _gaiusConfiguration = gaiusConfigurationOptions.Value;
        }

        public void PrintOperationTree(TreeNode<FSOperation> rootNode)
        {
            Console.WriteLine();

            var maxSrcLength = 0;

            foreach(var opNode in rootNode)
            {
                if(opNode.Data.OperationType == OperationType.Delete)
                    continue;

                var srcLength = (3 * opNode.Level) + opNode.Data.SourceDisplay.Length;
                
                if(srcLength > maxSrcLength)
                    maxSrcLength = srcLength;
            }

            foreach(var opNode in rootNode)
            {
                PrintOperationTreeNode(opNode, maxSrcLength);
            }

            if(rootNode.Data.Status == OperationStatus.Pending)
            {
                var areInvalidOps = rootNode.Any(node => node.Data.IsInvalid);
                var areUnsafeOps = rootNode.Any(node => node.Data.IsUnsafe);

                if(areInvalidOps || areUnsafeOps)
                {
                    Console.WriteLine();

                    if(areInvalidOps)
                        PrintInvalidOperationsMessages(rootNode.Where(node => node.Data.IsInvalid).ToList());

                    if(areUnsafeOps)
                        PrintUnsafeOperationsMessages(rootNode.Where(node => node.Data.IsUnsafe).ToList());
                }

                else PrintSafeOperationsMessage();
            }

            if(rootNode.Data.Status == OperationStatus.Complete)
            {
                PrintGenerationResultStatistics(rootNode);
            }
        }

        private void PrintOperationTreeNode(TreeNode<FSOperation> opNode, int maxSrcLength)
        {
            if(opNode.IsRoot && opNode.Data.Status == OperationStatus.Pending)
                PrintRootOperationTreeNode(opNode);

            PrintChildOperationTreeNode(opNode, maxSrcLength);
        }

        private void PrintRootOperationTreeNode(TreeNode<FSOperation> opNode)
        {
            var siteDirectoryFullPath = opNode.Data.WorkerTask.FileSystemInfo.FullName;
            Console.WriteLine($"[ Site Directory] {siteDirectoryFullPath}");
            Console.WriteLine($"[ Src. Directory] {_gaiusConfiguration.SourceDirectoryFullPath}");
            Console.WriteLine($"[Theme Directory] {_gaiusConfiguration.NamedThemeDirectoryFullPath}");
            Console.WriteLine($"[ Gen. Directory] {_gaiusConfiguration.GenerationDirectoryFullPath}");
            Console.WriteLine();
        }

        private static void PrintChildOperationTreeNode(TreeNode<FSOperation> opNode, int maxSrcLength)
        {
            PrintOperationStatus(opNode.Data);

            if(opNode.Data.OperationType == OperationType.Delete
                    || opNode.Data.OperationType == OperationType.Keep)
                PrintOutputDisplayOnlyOperationTreeNode(opNode, maxSrcLength);

            else PrintNormalOperationTreeNode(opNode, maxSrcLength);
        }

        private static (string indent, string outdent) GetIndentAndOutdent(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            var srcName = treeNode.Data.OperationType != OperationType.Delete ? treeNode.Data.SourceDisplay : string.Empty;

            var paddingLeft = 3 * treeNode.Level;
            var indent = string.Empty;

            if(!treeNode.IsRoot)
                indent = INDENT.PadLeft(paddingLeft, ' ');

            var paddingRight = maxSrcLength - indent.Length - srcName.Length;
            var outdent = string.Empty.PadRight(paddingRight + 1, ' ');

            return (indent, outdent);
        }

        private static void PrintNormalOperationTreeNode(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            (string indent, string outdent) = GetIndentAndOutdent(treeNode, maxSrcLength);
            
            Console.Write(indent);
            Colorful.Console.Write(treeNode.Data.SourceDisplay, GetColorForTreeNode(treeNode, false));
            Console.Write(outdent);
            PrintOperation(treeNode.Data);

            if(!string.IsNullOrWhiteSpace(treeNode.Data.OutputDisplay))
            {
                Console.Write(indent);
                Colorful.Console.Write(treeNode.Data.OutputDisplay, GetColorForTreeNode(treeNode, true));
            }

            Console.WriteLine();
        }

        private static void PrintOutputDisplayOnlyOperationTreeNode(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            (string indent, string outdent) = GetIndentAndOutdent(treeNode, maxSrcLength);
            
            Console.Write(string.Empty.PadRight(maxSrcLength + 1, ' '));
            PrintOperation(treeNode.Data);
            Console.Write(indent);

            var targetColor = treeNode.Data.OperationType == OperationType.Delete ? RED_COLOR : CYAN_COLOR;

            Colorful.Console.Write(treeNode.Data.SourceDisplay, targetColor);
            Console.WriteLine();
        }

        private static void PrintOperationStatus(FSOperation op)
        {
            switch(op.Status)
            {
                case OperationStatus.Complete:
                    Console.Write("[ ");
                    Colorful.Console.Write("OK", GREEN_COLOR);
                    Console.Write(" ] ");
                    break;
                
                case OperationStatus.Error:
                    Console.Write("[ ");
                    Colorful.Console.Write("ER", RED_COLOR);
                    Console.Write(" ] ");
                    break;

                default:
                    Console.Write("[ ");
                    Colorful.Console.Write("..", GREY_COLOR);
                    Console.Write(" ] ");
                    break;              
            }
        }

        private static void PrintOperation(FSOperation op)
        {
            Console.Write("[");
            PrintPipelineOperationType(op);
            PrintOperationTypeLabel(op);
            PrintOperationTypeIcon(op);
            Console.Write("] ");
        }

        private static void PrintOperationTypeIcon(FSOperation op)
        {
            if(op.Status == OperationStatus.Error)
            {
                Colorful.Console.Write(" ! ", RED_COLOR);
                return;
            }

            switch (op.OperationType)
            {
                case OperationType.CreateOverwrite:
                    Colorful.Console.Write(" + ", GREEN_COLOR);
                    break;

                case OperationType.Keep:
                    Colorful.Console.Write(" ^ ", CYAN_COLOR);
                    break;

                case OperationType.Skip:
                    Colorful.Console.Write(" _ ", GREY_COLOR);
                    break;

                case OperationType.Root:
                    Colorful.Console.Write(" R ", MAGENTA_COLOR);
                    break;

                case OperationType.Delete:
                    Colorful.Console.Write(" x ", RED_COLOR);
                    break;

                case OperationType.Invalid:
                    Colorful.Console.Write(" I ", RED_COLOR);
                    break;

                case OperationType.Null:
                    Colorful.Console.Write(" . ", GREY_COLOR);
                    break;
            }
        }

        private static void PrintOperationTypeLabel(FSOperation op)
        {
            if(op.Status == OperationStatus.Error)
            {
                Colorful.Console.Write("error ", RED_COLOR);
                return;
            }

            switch (op.OperationType)
            {
                case OperationType.CreateOverwrite:
                    Console.Write("create");
                    break;

                case OperationType.Keep:
                    Console.Write("keep  ");
                    break;

                case OperationType.Skip:
                    Console.Write("skip  ");
                    break;
                    
                case OperationType.Root:
                    Console.Write("root  ");
                    break;

                case OperationType.Delete:
                    Console.Write("delete");
                    break;

                case OperationType.Invalid:
                    Console.Write("inval ");
                    break;

                case OperationType.Null:
                default:
                    Colorful.Console.Write("......", GREY_COLOR);
                    break;
            }
        }

        private static void PrintPipelineOperationType(FSOperation op)
        {
            if(op.Status == OperationStatus.Error)
            {
                Colorful.Console.Write(" ..... ", GREY_COLOR);
                return;
            }

            switch(op.OperationType)
            {
                case OperationType.CreateOverwrite:

                    switch(op.WorkerTask.WorkType)
                    {
                        case WorkType.Transform:
                            Console.Write(" trans ");
                            break;

                        default:
                            Console.Write(" copy  ");
                            break;
                    }
                    break;

                default:
                    Colorful.Console.Write(" ..... ", GREY_COLOR);
                    break;
            }
        }

        private static void PrintInvalidOperationsMessages(List<TreeNode<FSOperation>> invalidOps)
        {
            Colorful.Console.WriteLine("One or more of the proposed operations cannot be performed because of invalid data:", YELLOW_COLOR);
            Console.WriteLine();
            
            for(var i=0; i < invalidOps.Count; i++)
            {
                Colorful.Console.Write($"{i+1}. ", YELLOW_COLOR);
                Console.WriteLine(invalidOps[i].Data.WorkerTask.FileSystemInfo.FullName);
            }

            Console.WriteLine();
        }

        private static void PrintUnsafeOperationsMessages(List<TreeNode<FSOperation>> unsafeOps)
        {
            Colorful.Console.WriteLine("One or more of the proposed operations is considered unsafe and could lead to data loss.", RED_COLOR);
            Colorful.Console.WriteLine("The following files/directories will be deleted because they do not have corresponding filesystem objects in the source directory:", RED_COLOR);
            Console.WriteLine();
            
            for(var i=0; i < unsafeOps.Count; i++)
            {
                Colorful.Console.Write($"{i+1}. ", RED_COLOR);
                Console.WriteLine(unsafeOps[i].Data.WorkerTask.FileSystemInfo.FullName);
            }

            Console.WriteLine();
        }

        private static void PrintSafeOperationsMessage()
        {
            Console.WriteLine();
            Colorful.Console.WriteLine("All of the proposed operations are considered safe.", GREEN_COLOR);
        }

        private const string DIRECTORIES_WERE = "directories were";
        private const string DIRECTORY_WAS = "directory was";
        private const string FILES_WERE = "files were";
        private const string FILE_WAS = "file was";

        public static void PrintGenerationResultStatistics(TreeNode<FSOperation> treeNode)
        {
            var completedOps = treeNode.Where(node => node.Data.Status == OperationStatus.Complete).ToList();

            var dirsCreatedOverwritten = completedOps.Count(node => node.Data.OperationType == OperationType.CreateOverwrite && node.Data.IsDirectoryOp);
            var filesCreatedOverwritten = completedOps.Count(node => node.Data.OperationType == OperationType.CreateOverwrite && !node.Data.IsDirectoryOp);

            var dirsDeleted = completedOps.Count(node => node.Data.OperationType == OperationType.Delete && node.Data.IsDirectoryOp);
            var filesDeleted = completedOps.Count(node => node.Data.OperationType == OperationType.Delete && !node.Data.IsDirectoryOp);

            var dirsSkipped = treeNode.Count(node => node.Data.OperationType == OperationType.Skip && node.Data.IsDirectoryOp);
            var filesSkipped = treeNode.Count(node => node.Data.OperationType == OperationType.Skip && !node.Data.IsDirectoryOp);

            var invalidFiles = treeNode.Count(node => node.Data.OperationType == OperationType.Invalid);

            var errorOps = treeNode.Where(node => node.Data.Status == OperationStatus.Error).ToList();

            Console.WriteLine();

            if(completedOps.Count > 0)
            {
                Colorful.Console.WriteLine($"{completedOps.Count} operations were completed successfully.", GREEN_COLOR);
                
                Console.WriteLine();
                Console.WriteLine("Create/Overwrite Operations:");
                Console.WriteLine($"  {dirsCreatedOverwritten} new {(dirsCreatedOverwritten == 1 ? DIRECTORY_WAS : DIRECTORIES_WERE)} created/overwritten.");
                Console.WriteLine($"  {filesCreatedOverwritten} new {(filesCreatedOverwritten == 1 ? FILE_WAS : FILES_WERE)} created/overwritten.");

                Console.WriteLine();
                Console.WriteLine("Delete Operations:");
                Console.WriteLine($"  {dirsDeleted} {(dirsDeleted == 1 ? DIRECTORY_WAS : DIRECTORIES_WERE)} deleted.");
                Console.WriteLine($"  {filesDeleted} {(filesDeleted == 1 ? FILE_WAS : FILES_WERE)} deleted.");

                Console.WriteLine();
                Console.WriteLine("Skipped Operations:");
                Console.WriteLine($"  {dirsSkipped} {(dirsSkipped == 1 ? DIRECTORY_WAS : DIRECTORIES_WERE)} skipped.");
                Console.WriteLine($"  {filesSkipped} {(filesSkipped == 1 ? FILE_WAS : FILES_WERE)} skipped.");

                Console.WriteLine();
                Console.WriteLine("Invalid Operations:");
                Console.WriteLine($"  {invalidFiles} {(invalidFiles == 1 ? FILE_WAS : FILES_WERE)} invalid.");
            }

            if(errorOps.Count > 0)
            {
                Console.WriteLine();
                Colorful.Console.WriteLine($"{errorOps.Count} operation{(errorOps.Count == 1 ? "s" : string.Empty)} did not complete because of errors.");
            }

            Console.WriteLine();
        }

        private static Color GetColorForTreeNode(TreeNode<FSOperation> op, bool isOutputDisplay)
        {
            var workerTask = op.Data?.WorkerTask;

            if(workerTask == null)
                return GREY_COLOR;

            if(workerTask.FileSystemInfo.IsDirectory())
                return DIRECTORY_COLOR;

            if(workerTask.FileSystemInfo.IsFile())
                return GetColorFromFileName(isOutputDisplay ? workerTask.TaskFileOrDirectoryName : workerTask.FileSystemInfo.Name);

            return GREY_COLOR;
        }

        private static Color GetColorFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            switch(extension)
            {
                case ".md":
                    return MARKDOWN_COLOR;

                case ".html":
                    return HTML_COLOR;

                case ".liquid":
                    return LIQUID_COLOR;

                case ".css":
                case ".less":
                case ".sass":
                    return CSS_LESS_SASS_COLOR;

                case ".js":
                    return JS_COLOR;

                default:
                    return FILE_COLOR;
            }
        }
            
        public void PrintDefault()
        {
            PrintStylizedApplicationName();
            TerminalUtilities.PrintApplicationNameAndVersion();
            PrintUsage();
        }

        public void PrintHelpCommand()
        {
            PrintUsage();
        }

        public void PrintVersionCommand()
        {
            PrintStylizedApplicationName();
            TerminalUtilities.PrintApplicationNameAndVersion();
        }

        public void PrintShowConfigurationCommand(string path)
        {
            Console.WriteLine();
            PrintConfiguration(path, WHITE_COLOR);
        }

        public void PrintUnknownCommand(string command)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine($"Unrecognized command or argument '{command}'.", RED_COLOR);
            PrintUsage();
        }

        public void PrintMissingArgument(string argument, string command)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine($"Missing '{argument}' argument for the '{command}' command.", RED_COLOR);
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
        
        public void PrintSiteContainerDirectoryNotValid(List<string> validationErrors)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine($"The site container directory '{_gaiusConfiguration.SiteContainerFullPath}' is not valid.", RED_COLOR);
            System.Console.WriteLine();
            Colorful.Console.WriteLine("Validation errors:", RED_COLOR);

            for(var i = 0; i < validationErrors.Count; i++)
            {
                Colorful.Console.WriteLine($"{i+1}. {validationErrors[i]}", RED_COLOR);
            }

            System.Console.WriteLine();
        }

        private void PrintConfiguration(string path, Color jsonColor)
        {
            Console.WriteLine($"Current configuration defined in {path}/gaius.json:");
            Colorful.Console.WriteLine($"Note: if no gaius.json exists in {path}, default configuration values are used.", GREY_COLOR);
            Console.WriteLine();
            Colorful.Console.WriteLine(JsonConvert.SerializeObject(_gaiusConfiguration, Formatting.Indented), jsonColor);
            System.Console.WriteLine();
        }

        private static void PrintUsage()
        {
            var usage = 
@"
Usage: gaius [command] [options]

Commands (Gaius Engine):

  version                Show version information.
  help                   Show help information.
  showconfig [path]      Show the configuration in [path].

  process-test [path]    Process the source data in [path] using the config file [path]/gaius.json.
                           This does *not* prepend the 'GenerationUrlRootPrefix' config param to URLs.
                           This allows for the local testing of generated sites (e.g. http://localhost).

  process [path]         Process the source data in [path] using the config file [path]/gaius.json.

Commands (Gaius Server):

  test                   Runs process-test command in current directory.
                         Starts a local web server which serves up generated site content in [path/_generated].
                            By default local web server is accessible on http://localhost:5000

Note: if no [path] is provided, [path] defaults to current directory.

Commands (CLI wrapper):

  update-all             Update the Gaius engine binaries, Gaius server binaries,
                           Github Actions workflow, and the CLI wrappers (recommended).

  update-engine          Only update the Gaius engine binaries.
  update-server          Only update the Gaius server binaries.
  update-cli             Only update the Gaius CLI wrappers.
  update-github-actions  Only update the Gaius Github Actions workflow.

Options:
  -y                     Automatically answer 'yes' for all questions.
                           This allows for automatic processing of source data.
";
            System.Console.WriteLine(usage);
        }

        public void PrintInvalidConfiguration(string path)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine("Unsupported configuration detected.", RED_COLOR);
            Console.WriteLine("Gaius currently supports the following configurations:");
            System.Console.WriteLine();
            Console.WriteLine($"Workers: {string.Join(",", _gaiusConfiguration.SupportedWorkers)}");

            var checkMsg = 
@"
Please check your gaius.json file and make sure the following properties are set correctly:

    * Processor
    * Pipeline
";
            
            Console.WriteLine(checkMsg);
            PrintConfiguration(path, RED_COLOR);
            Console.WriteLine();
            Console.WriteLine("Example gaius.json file:");

            var exampleJson =
@"
{
  ""SourceDirectoryName"" : ""_source"",
  ""LayoutDirectoryName"" : ""_layouts"",
  ""GenerationDirectoryName"" : ""_generated"",
  ""Processor"" : ""file"",
  ""Pipeline"": ""markdown-liquid""
}
";
            Colorful.Console.WriteLine(exampleJson, GREEN_COLOR);
        }
    }
}