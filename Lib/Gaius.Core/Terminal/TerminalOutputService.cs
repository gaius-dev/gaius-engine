using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Gaius.Core.Configuration;
using Gaius.Core.Processing;
using Gaius.Core.Processing.FileSystem;
using Strube.Utilities.Colors;
using Strube.Utilities.Reflection;
using Strube.Utilities.DataStructures;
using Strube.Utilities.Terminal;
using Figgle;
using Newtonsoft.Json;
using Gaius.Core.Worker;

namespace Gaius.Core.Terminal
{
    public class TerminalOutputService : ITerminalOutputService
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

        public TerminalOutputService(IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            _gaiusConfiguration = gaiusConfigurationOptions.Value;
        }

        public void PrintOperationTree(TreeNode<FSOperation> opTree)
        {
            Console.WriteLine();

            var maxSrcLength = 0;

            foreach(var treeNode in opTree)
            {
                if(treeNode.Data.FSOperationType == FSOperationType.Delete)
                    continue;

                var srcLength = (3 * treeNode.Level) + treeNode.Data.FSInfo.Name.Length;
                
                if(srcLength > maxSrcLength)
                    maxSrcLength = srcLength;
            }

            foreach(var treeNode in opTree)
            {
                PrintOperationTreeNode(treeNode, maxSrcLength);
            }

            if(opTree.Data.Status == OperationStatus.Pending)
            {
                if(opTree.Any(node => node.Data.IsUnsafe))
                    PrintUnsafeOperationsMessages(opTree.Where(node => node.Data.IsUnsafe).ToList());

                else
                    PrintSafeOperationsMessage();
            }

            if(opTree.Data.Status == OperationStatus.Complete)
            {
                PrintGenerationResultStatistics(opTree);
            }
        }

        private void PrintOperationTreeNode(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            if(treeNode.IsRoot && treeNode.Data.Status == OperationStatus.Pending)
                PrintRootOperationTreeNode(treeNode);

            PrintChildOperationTreeNode(treeNode, maxSrcLength);
        }

        private void PrintRootOperationTreeNode(TreeNode<FSOperation> treeNode)
        {
            var sourceDirectoryFullPath = treeNode.Data.FSInfo.FullName;
            Console.WriteLine($"[Source Directory] {sourceDirectoryFullPath}");
            Console.WriteLine($"[Layout Directory] {_gaiusConfiguration.LayoutDirectorFullPath}");
            Console.WriteLine($"[ Gen. Directory ] {_gaiusConfiguration.GenerationDirectoryFullPath}");
            Console.WriteLine();
        }

        private static void PrintChildOperationTreeNode(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            PrintOperationStatus(treeNode.Data);

            if(treeNode.Data.FSOperationType == FSOperationType.CreateNew)
                PrintCreateNewOperationTreeNode(treeNode, maxSrcLength);

            else if(treeNode.Data.FSOperationType == FSOperationType.Overwrite)
                PrintOverwriteOperationTreeNode(treeNode, maxSrcLength);

            else if(treeNode.Data.FSOperationType == FSOperationType.Delete)
                PrintDeleteOperationTreeNode(treeNode, maxSrcLength);
            
            else if(treeNode.Data.FSOperationType == FSOperationType.Skip)
                PrintSkipOperationTreeNode(treeNode, maxSrcLength);
        }

        private static (string indent, string outdent) GetIndentAndOutdent(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            var srcName = treeNode.Data.FSOperationType != FSOperationType.Delete ? treeNode.Data.FSInfo.Name : string.Empty;

            var paddingLeft = 3 * treeNode.Level;
            var indent = string.Empty;

            if(!treeNode.IsRoot)
                indent = INDENT.PadLeft(paddingLeft, ' ');

            var paddingRight = maxSrcLength - indent.Length - srcName.Length;
            var outdent = string.Empty.PadRight(paddingRight + 1, ' ');

            return (indent, outdent);
        }

        private static void PrintCreateNewOperationTreeNode(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            (string indent, string outdent) = GetIndentAndOutdent(treeNode, maxSrcLength);
            
            Console.Write(indent);
            Colorful.Console.Write(treeNode.Data.FSInfo.Name, GetColorForTreeNodeSource(treeNode));
            Console.Write(outdent);
            PrintOperation(treeNode.Data);
            Console.Write(indent);
            Colorful.Console.Write(treeNode.Data.WorkerTask.Target, GetColorForTreeNodeDestination(treeNode));
            Console.WriteLine();
        }

        private static void PrintOverwriteOperationTreeNode(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            (string indent, string outdent) = GetIndentAndOutdent(treeNode, maxSrcLength);

            Console.Write(indent);
            Colorful.Console.Write(treeNode.Data.FSInfo.Name, GetColorForTreeNodeSource(treeNode));
            Console.Write(outdent);
            PrintOperation(treeNode.Data);
            Console.Write(indent);
            Colorful.Console.Write(treeNode.Data.WorkerTask.Target, GetColorForTreeNodeDestination(treeNode));
            Console.WriteLine();
        }

        private static void PrintDeleteOperationTreeNode(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            (string indent, string outdent) = GetIndentAndOutdent(treeNode, maxSrcLength);
            
            Console.Write(string.Empty.PadRight(maxSrcLength + 1, ' '));
            PrintOperation(treeNode.Data);
            Console.Write(indent);
            Colorful.Console.Write(treeNode.Data.FSInfo.Name, RED_COLOR);
            Console.WriteLine();
        }

        private static void PrintSkipOperationTreeNode(TreeNode<FSOperation> treeNode, int maxSrcLength)
        {
            (string indent, string outdent) = GetIndentAndOutdent(treeNode, maxSrcLength);

            Console.Write(indent);
            Colorful.Console.Write(treeNode.Data.FSInfo.Name, GetColorForTreeNodeSource(treeNode));
            Console.Write(outdent);
            PrintOperation(treeNode.Data);
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

            switch (op.FSOperationType)
            {
                case FSOperationType.CreateNew:
                    Colorful.Console.Write(" + ", GREEN_COLOR);
                    break;

                case FSOperationType.Overwrite:
                    Colorful.Console.Write(" @ ", GREEN_COLOR);
                    break;

                case FSOperationType.Skip:
                    Colorful.Console.Write(" _ ", GREY_COLOR);
                    break;

                case FSOperationType.Delete:
                    Colorful.Console.Write(" x ", RED_COLOR);
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

            switch (op.FSOperationType)
            {
                case FSOperationType.Delete:
                    Console.Write("delete");
                    break;

                case FSOperationType.CreateNew:
                    Console.Write("create");
                    break;

                case FSOperationType.Overwrite:
                    Console.Write("overwr");
                    break;

                case FSOperationType.Skip:
                    Console.Write("skip  ");
                    break;
            }
        }

        private static void PrintPipelineOperationType(FSOperation op)
        {
            if(op.Status == OperationStatus.Error)
            {
                Colorful.Console.Write(" ....  ", GREY_COLOR);
                return;
            }

            switch(op.FSOperationType)
            {
                case FSOperationType.CreateNew:
                case FSOperationType.Overwrite:

                    switch(op.WorkerTask.TransformType)
                    {
                        case WorkType.Transform:
                            Console.Write(" tran  ");
                            break;

                        case WorkType.Transpile:
                            Console.Write(" tpile ");
                            break;

                        default:
                            Console.Write(" copy  ");
                            break;
                    }
                    break;

                default:
                    Colorful.Console.Write(" ....  ", GREY_COLOR);
                    break;
            }
        }

        private static void PrintUnsafeOperationsMessages(List<TreeNode<FSOperation>> unsafeOps)
        {
            Console.WriteLine();
            Colorful.Console.WriteLine("One or more of the proposed operations is considered unsafe and could lead to data loss.", RED_COLOR);
            Colorful.Console.WriteLine("The following files/directories will be deleted because they do not have corresponding filesystem objects in the source directory:", RED_COLOR);
            Console.WriteLine();
            
            for(var i=0; i < unsafeOps.Count; i++)
            {
                Colorful.Console.Write($"{i+1}. ", RED_COLOR);
                Console.WriteLine(unsafeOps[i].Data.FSInfo.FullName);
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

            var dirsCreated = completedOps.Count(node => node.Data.FSOperationType == FSOperationType.CreateNew && node.Data.IsDirectoryOp);
            var filesCreated = completedOps.Count(node => node.Data.FSOperationType == FSOperationType.CreateNew && !node.Data.IsDirectoryOp);

            var dirsOverwritten = completedOps.Count(node => node.Data.FSOperationType == FSOperationType.Overwrite && node.Data.IsDirectoryOp);
            var filesOverwritten = completedOps.Count(node => node.Data.FSOperationType == FSOperationType.Overwrite && !node.Data.IsDirectoryOp);

            var dirsDeleted = completedOps.Count(node => node.Data.FSOperationType == FSOperationType.Delete && node.Data.IsDirectoryOp);
            var filesDeleted = completedOps.Count(node => node.Data.FSOperationType == FSOperationType.Delete && !node.Data.IsDirectoryOp);

            var dirsSkipped = treeNode.Count(node => node.Data.FSOperationType == FSOperationType.Skip && node.Data.IsDirectoryOp);
            var filesSkipped = treeNode.Count(node => node.Data.FSOperationType == FSOperationType.Skip && !node.Data.IsDirectoryOp);

            var errorOps = treeNode.Where(node => node.Data.Status == OperationStatus.Error).ToList();

            Console.WriteLine();

            if(completedOps.Count > 0)
            {
                Colorful.Console.WriteLine($"{completedOps.Count} operations were completed successfully.", GREEN_COLOR);
                
                Console.WriteLine();
                Console.WriteLine("Create Operations:");
                Console.WriteLine($"  {dirsCreated} new {(dirsCreated == 1 ? DIRECTORY_WAS : DIRECTORIES_WERE)} created.");
                Console.WriteLine($"  {filesCreated} new {(filesCreated == 1 ? FILE_WAS : FILES_WERE)} created.");

                Console.WriteLine();
                Console.WriteLine("Overwrite Operations:");
                Console.WriteLine($"  {dirsOverwritten} {(dirsOverwritten == 1 ? DIRECTORY_WAS : DIRECTORIES_WERE)} overwritten.");
                Console.WriteLine($"  {filesOverwritten} {(filesOverwritten == 1 ? FILE_WAS : FILES_WERE)} overwritten.");

                Console.WriteLine();
                Console.WriteLine("Delete Operations:");
                Console.WriteLine($"  {dirsDeleted} {(dirsDeleted == 1 ? DIRECTORY_WAS : DIRECTORIES_WERE)} deleted.");
                Console.WriteLine($"  {filesDeleted} {(filesDeleted == 1 ? FILE_WAS : FILES_WERE)} deleted.");

                Console.WriteLine();
                Console.WriteLine("Skipped Operations:");
                Console.WriteLine($"  {dirsSkipped} {(dirsSkipped == 1 ? DIRECTORY_WAS : DIRECTORIES_WERE)} skipped.");
                Console.WriteLine($"  {filesSkipped} {(filesSkipped == 1 ? FILE_WAS : FILES_WERE)} skipped.");
            }

            if(errorOps.Count > 0)
            {
                Console.WriteLine();
                Colorful.Console.WriteLine($"{errorOps.Count} operation{(errorOps.Count == 1 ? "s" : string.Empty)} did not complete because of errors.");
            }

            Console.WriteLine();
        }

        private static Color GetColorForTreeNodeSource(TreeNode<FSOperation> op)
        {
            var fsInfo = op.Data.FSInfo;

            if(fsInfo is DirectoryInfo)
                return DIRECTORY_COLOR;

            if(fsInfo is FileInfo)
                return GetColorFromFileName(fsInfo.Name);

            return FILE_COLOR;
        }

        private static Color GetColorForTreeNodeDestination(TreeNode<FSOperation> op)
        {
            var fsInfo = op.Data.FSInfo;

            if(fsInfo is FileInfo)
                return GetColorFromFileName(op.Data.WorkerTask.Target);
            
            return GetColorForTreeNodeSource(op);
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
            Console.WriteLine();
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
            var assemblyTitleFiggle = FiggleFonts.Standard.Render(AssemblyUtilities.GetAssemblyTitle(AssemblyUtilities.EntryAssembly));
            Colorful.Console.WriteLine(assemblyTitleFiggle, Color.Gold);
        }
        
        public void PrintSiteContainerDirectoryNotValid(List<string> validationErrors)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine($"The site container directory '{_gaiusConfiguration.SiteContainerPath}' is not valid.", RED_COLOR);
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

Commands:
    version             Show version information.
    help                Show a reference for how to use gaius.
    showconfig [path]   Show the current configuration at the location specified by [path]
    process [path]      Process the location specified by [path] using the configuration [path]/gaius.json

Options:
    -y                  Assume 'yes' as the answer for all questions.

Example: gaius process ./mysite -y
         Processes the directory ./mysite using the configuration ./mysite/gaius.json
";
            System.Console.WriteLine(usage);
        }

        public void PrintInvalidConfiguration(string path)
        {
            System.Console.WriteLine();
            Colorful.Console.WriteLine("Unsupported configuration detected.", RED_COLOR);
            Console.WriteLine("Gaius currently supports the following configurations:");
            System.Console.WriteLine();
            Console.WriteLine($"Processors: {string.Join(",", _gaiusConfiguration.SupportedProcessors)}");
            Console.WriteLine($"Pipelines: {string.Join(",", _gaiusConfiguration.SupportedPipelines)}");

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