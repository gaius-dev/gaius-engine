using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Gaius.Core.Configuration;
using Gaius.Core.DataStructures;
using Gaius.Core.Terminal;
using Gaius.Worker;
using Gaius.Core.FileSystem;

namespace Gaius.Processing.Display
{
    public class TerminalDisplayService : ITerminalDisplayService
    {
        private const string _indent = "|_ ";
        
        private GaiusConfiguration _gaiusConfiguration;

        public TerminalDisplayService(IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            _gaiusConfiguration = gaiusConfigurationOptions.Value;
        }

        public void PrintOperationTree(TreeNode<IOperation> rootNode)
        {
            Console.WriteLine();

            var maxSrcLength = 0;

            foreach(var opNode in rootNode)
            {
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

                if(areInvalidOps)
                {
                    Console.WriteLine();

                    if(areInvalidOps)
                        PrintInvalidOperationsMessages(rootNode.Where(node => node.Data.IsInvalid).ToList());
                }

                else PrintSafeOperationsMessage();
            }

            if(rootNode.Data.Status == OperationStatus.Complete)
            {
                PrintGenerationResultStatistics(rootNode);
            }
        }

        private void PrintOperationTreeNode(TreeNode<IOperation> opNode, int maxSrcLength)
        {
            if(opNode.IsRoot && opNode.Data.Status == OperationStatus.Pending)
                PrintRootOperationTreeNode(opNode);

            PrintChildOperationTreeNode(opNode, maxSrcLength);
        }

        private void PrintRootOperationTreeNode(TreeNode<IOperation> opNode)
        {
            var siteDirectoryFullPath = opNode.Data.WorkerTask.FileSystemInfo.FullName;
            Console.WriteLine($"[ Site Directory] {siteDirectoryFullPath}");
            Console.WriteLine($"[ Src. Directory] {_gaiusConfiguration.SourceDirectoryFullPath}");
            Console.WriteLine($"[Theme Directory] {_gaiusConfiguration.NamedThemeDirectoryFullPath}");
            Console.WriteLine($"[ Gen. Directory] {_gaiusConfiguration.GenerationDirectoryFullPath}");
            Console.WriteLine($"[  Testmode On  ] {_gaiusConfiguration.IsTestModeEnabled}");
            Console.WriteLine();
        }

        private static void PrintChildOperationTreeNode(TreeNode<IOperation> opNode, int maxSrcLength)
        {
            PrintOperationStatus(opNode.Data);
            PrintNormalOperationTreeNode(opNode, maxSrcLength);
        }

        private static (string indent, string outdent) GetIndentAndOutdent(TreeNode<IOperation> treeNode, int maxSrcLength)
        {
            var srcName = treeNode.Data.SourceDisplay;

            var paddingLeft = 3 * treeNode.Level;
            var indent = string.Empty;

            if(!treeNode.IsRoot)
                indent = _indent.PadLeft(paddingLeft, ' ');

            var paddingRight = maxSrcLength - indent.Length - srcName.Length;
            var outdent = string.Empty.PadRight(paddingRight + 1, ' ');

            return (indent, outdent);
        }

        private static void PrintNormalOperationTreeNode(TreeNode<IOperation> treeNode, int maxSrcLength)
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

        private static void PrintOperationStatus(IOperation op)
        {
            switch(op.Status)
            {
                case OperationStatus.Complete:
                    Console.Write("[ ");
                    Colorful.Console.Write("OK", TerminalUtilities._colorGreen);
                    Console.Write(" ] ");
                    break;
                
                case OperationStatus.Error:
                    Console.Write("[ ");
                    Colorful.Console.Write("ER", TerminalUtilities._colorRed);
                    Console.Write(" ] ");
                    break;

                default:
                    Console.Write("[ ");
                    Colorful.Console.Write("..", TerminalUtilities._colorGrey);
                    Console.Write(" ] ");
                    break;              
            }
        }

        private static void PrintOperation(IOperation op)
        {
            Console.Write("[");
            PrintPipelineOperationType(op);
            PrintOperationTypeLabel(op);
            PrintOperationTypeIcon(op);
            Console.Write("] ");
        }

        private static void PrintOperationTypeIcon(IOperation op)
        {
            if(op.Status == OperationStatus.Error)
            {
                Colorful.Console.Write(" ! ", TerminalUtilities._colorRed);
                return;
            }

            switch (op.OperationType)
            {
                case OperationType.CreateOverwrite:
                    Colorful.Console.Write(" + ", TerminalUtilities._colorGreen);
                    break;

                case OperationType.Skip:
                    Colorful.Console.Write(" _ ", TerminalUtilities._colorGrey);
                    break;

                case OperationType.Root:
                    Colorful.Console.Write(" R ", TerminalUtilities._colorMagenta);
                    break;

                case OperationType.Invalid:
                    Colorful.Console.Write(" I ", TerminalUtilities._colorRed);
                    break;

                case OperationType.Null:
                    Colorful.Console.Write(" . ", TerminalUtilities._colorGrey);
                    break;
            }
        }

        private static void PrintOperationTypeLabel(IOperation op)
        {
            if(op.Status == OperationStatus.Error)
            {
                Colorful.Console.Write("error ", TerminalUtilities._colorRed);
                return;
            }

            switch (op.OperationType)
            {
                case OperationType.CreateOverwrite:
                    Console.Write("create");
                    break;

                case OperationType.Skip:
                    Console.Write("skip  ");
                    break;
                    
                case OperationType.Root:
                    Console.Write("root  ");
                    break;

                case OperationType.Invalid:
                    Console.Write("inval ");
                    break;

                case OperationType.Null:
                default:
                    Colorful.Console.Write("......", TerminalUtilities._colorGrey);
                    break;
            }
        }

        private static void PrintPipelineOperationType(IOperation op)
        {
            if(op.Status == OperationStatus.Error)
            {
                Colorful.Console.Write(" ..... ", TerminalUtilities._colorGrey);
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
                    Colorful.Console.Write(" ..... ", TerminalUtilities._colorGrey);
                    break;
            }
        }

        private static void PrintInvalidOperationsMessages(List<TreeNode<IOperation>> invalidOps)
        {
            Colorful.Console.WriteLine("One or more of the proposed operations cannot be performed because of invalid data:", TerminalUtilities._colorYellow);
            Console.WriteLine();
            
            for(var i=0; i < invalidOps.Count; i++)
            {
                Colorful.Console.Write($"{i+1}. ", TerminalUtilities._colorYellow);
                Console.WriteLine(invalidOps[i].Data.WorkerTask.FileSystemInfo.FullName);
            }

            Console.WriteLine();
        }

        private static void PrintSafeOperationsMessage()
        {
            Console.WriteLine();
            Colorful.Console.WriteLine("All of the proposed operations are considered safe.", TerminalUtilities._colorGreen);
        }

        private const string DIRECTORIES_WERE = "directories were";
        private const string DIRECTORY_WAS = "directory was";
        private const string FILES_WERE = "files were";
        private const string FILE_WAS = "file was";

        public static void PrintGenerationResultStatistics(TreeNode<IOperation> treeNode)
        {
            var completedOps = treeNode.Where(node => node.Data.Status == OperationStatus.Complete).ToList();

            var dirsCreatedOverwritten = completedOps.Count(node => node.Data.OperationType == OperationType.CreateOverwrite && node.Data.IsDirectoryOp);
            var filesCreatedOverwritten = completedOps.Count(node => node.Data.OperationType == OperationType.CreateOverwrite && !node.Data.IsDirectoryOp);

            var dirsSkipped = treeNode.Count(node => node.Data.OperationType == OperationType.Skip && node.Data.IsDirectoryOp);
            var filesSkipped = treeNode.Count(node => node.Data.OperationType == OperationType.Skip && !node.Data.IsDirectoryOp);

            var invalidFiles = treeNode.Count(node => node.Data.OperationType == OperationType.Invalid);

            var errorOps = treeNode.Where(node => node.Data.Status == OperationStatus.Error).ToList();

            Console.WriteLine();

            if(completedOps.Count > 0)
            {
                Colorful.Console.WriteLine($"{completedOps.Count} operations were completed successfully.", TerminalUtilities._colorGreen);
                
                Console.WriteLine();
                Console.WriteLine("Create/Overwrite Operations:");
                Console.WriteLine($"  {dirsCreatedOverwritten} new {(dirsCreatedOverwritten == 1 ? DIRECTORY_WAS : DIRECTORIES_WERE)} created/overwritten.");
                Console.WriteLine($"  {filesCreatedOverwritten} new {(filesCreatedOverwritten == 1 ? FILE_WAS : FILES_WERE)} created/overwritten.");

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

        private static Color GetColorForTreeNode(TreeNode<IOperation> op, bool isOutputDisplay)
        {
            var workerTask = op.Data?.WorkerTask;

            if(workerTask == null)
                return TerminalUtilities._colorGrey;

            if(workerTask.FileSystemInfo.IsDirectory())
                return TerminalUtilities._colorDirectory;

            if(workerTask.FileSystemInfo.IsFile())
                return GetColorFromFileName(isOutputDisplay ? workerTask.TaskFileOrDirectoryName : workerTask.FileSystemInfo.Name);

            return TerminalUtilities._colorGrey;
        }

        private static Color GetColorFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            switch(extension)
            {
                case ".md":
                    return TerminalUtilities._colorFileMarkdown;

                case ".html":
                    return TerminalUtilities._colorFileHtml;

                case ".liquid":
                    return TerminalUtilities._colorFileLiquid;

                case ".css":
                case ".less":
                case ".sass":
                    return TerminalUtilities._colorFileCssLess;

                case ".js":
                    return TerminalUtilities._colorFileJavascript;

                default:
                    return TerminalUtilities._colorFile;
            }
        }
    }        
}