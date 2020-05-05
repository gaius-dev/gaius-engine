using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Gaius.Core.Configuration;
using Strube.Utilities.DataStructures;
using Strube.Utilities.FileSystem;
using Gaius.Core.Worker;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSProcessor : IFSProcessor
    {
        private readonly IServiceProvider _provider;
        private readonly IWorker _worker;
        private readonly GaiusConfiguration _gaiusConfiguration;

        public FSProcessor(IServiceProvider provider, IWorker worker, IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            _provider = provider;
            _worker = worker;
            _gaiusConfiguration = gaiusConfigurationOptions.Value;
        }

        public (bool, List<string>) ValidateSiteContainerDir()
        {
            var validationErrors = new List<string>();

            var sourceDirExists = Directory.Exists(_gaiusConfiguration.SourceDirectoryFullPath);

            if (!sourceDirExists)
                validationErrors.Add($"The source directory '{_gaiusConfiguration.SourceDirectoryFullPath}' does not exist.");

            var otherReqDirsExist = true;
            var requiredDirsFullPaths = new List<string>();

            switch(_gaiusConfiguration.Pipeline)
            {
                case GaiusConfiguration.MARKDOWN_LIQUIDE_PIPELINE:
                default:
                    requiredDirsFullPaths.Add(_gaiusConfiguration.LayoutDirectorFullPath);
                    break;
            }

            foreach(var reqDirFullPath in requiredDirsFullPaths)
            {
                if(!Directory.Exists(reqDirFullPath))
                {
                    otherReqDirsExist = false;
                    validationErrors.Add($"The required directory '{reqDirFullPath}' does not exist.");
                }
            }

            return (sourceDirExists && otherReqDirsExist, validationErrors);
        }

        public TreeNode<FSOperation> CreateFSOperationTree()
        {
            var rootSourceDir = new DirectoryInfo(_gaiusConfiguration.SourceDirectoryFullPath);
            var rootGenDirFullPath = _gaiusConfiguration.GenerationDirectoryFullPath;

            FSOperation rootOp = null;
            DirectoryInfo rootGenDir = null;

            if(!Directory.Exists(rootGenDirFullPath))
                rootOp = FSOperation.CreateInstance(_provider, rootSourceDir, FSOperationType.CreateNew);
                
            else
            {
                rootOp = FSOperation.CreateInstance(_provider, rootSourceDir, FSOperationType.Overwrite);
                rootGenDir = new DirectoryInfo(rootGenDirFullPath);
            }
            
            var opTree = new TreeNode<FSOperation>(rootOp);
            AddOperationsToTreeNode(opTree, rootSourceDir, rootGenDir);
            return opTree;            
        }

        private void AddOperationsToTreeNode(TreeNode<FSOperation> startNode, DirectoryInfo sourceStartDir, DirectoryInfo genStartDir)
        {
            var existingGenFiles = new List<FileInfo>();

            foreach(var sourceFile in sourceStartDir?.EnumerateFiles() ?? Enumerable.Empty<FileInfo>())
            {
                var existingGenFile = FindMachingGenerationFile(genStartDir, sourceFile);

                if(existingGenFile == null)
                {
                    startNode.AddChild(FSOperation.CreateInstance(_provider, sourceFile, FSOperationType.CreateNew));
                }

                else
                {
                    existingGenFiles.Add(existingGenFile);
                    startNode.AddChild(FSOperation.CreateInstance(_provider, sourceFile, FSOperationType.Overwrite));
                }
            }

            foreach(var genFile in genStartDir?.EnumerateFiles()
                .Where(file => !existingGenFiles.Any(exFile => exFile.Name.Equals(file.Name))) ?? Enumerable.Empty<FileInfo>())
            {
                startNode.AddChild(FSOperation.CreateInstance(_provider, genFile, FSOperationType.Delete));
            }

            var existingGenDirs = new List<DirectoryInfo>();

            foreach(var sourceDir in sourceStartDir?.EnumerateDirectories() ?? Enumerable.Empty<DirectoryInfo>())
            {
                var existingGenDir = FindMatchingGenerationDirectory(genStartDir, sourceDir);

                if(existingGenDir == null)
                {
                    var newOpTreeNode = startNode.AddChild(FSOperation.CreateInstance(_provider, sourceDir, FSOperationType.CreateNew));
                    AddOperationsToTreeNode(newOpTreeNode, sourceDir, null);
                }
                    
                else
                {
                    existingGenDirs.Add(existingGenDir);
                    var newOpTreeNode = startNode.AddChild(FSOperation.CreateInstance(_provider, sourceDir, FSOperationType.Overwrite));
                    AddOperationsToTreeNode(newOpTreeNode, sourceDir, existingGenDir);
                }                
            }

            foreach(var genDir in genStartDir?.EnumerateDirectories()
                .Where(dir => !existingGenDirs.Any(exDir=> exDir.Name.Equals(dir.Name))) ?? Enumerable.Empty<DirectoryInfo>())
            {
                var newOpTreeNode = startNode.AddChild(FSOperation.CreateInstance(_provider, genDir, FSOperationType.Delete));
            }
        }

        private FileInfo FindMachingGenerationFile(DirectoryInfo genStartDir, FileInfo sourceFile)
        {
            if(genStartDir == null)
                return null;

            return genStartDir.EnumerateFiles()
                .FirstOrDefault(genFile => genFile.Name.Equals(_worker.GetTarget(sourceFile)));
        }

        private static DirectoryInfo FindMatchingGenerationDirectory(DirectoryInfo genStartDir, DirectoryInfo sourceDir)
        {
            if(genStartDir == null)
                return null;

            return genStartDir.EnumerateDirectories().FirstOrDefault(genDir => genDir.Name.Equals(sourceDir.Name));
        }

        public void ProcessFSOperationTree(TreeNode<FSOperation> opTree)
        {
            var genDirFullPath = _gaiusConfiguration.GenerationDirectoryFullPath;

            if(Directory.Exists(genDirFullPath))
            {
                var genDir = new DirectoryInfo(genDirFullPath);
                var allContainedFsInfos = genDir.EnumerateFileSystemInfos();

                //rs: delete *almost* all contained directories and files in the generation directory
                foreach(var containedFsInfo in allContainedFsInfos)
                {
                    if(containedFsInfo.IsDirectory())
                    {
                        //rs: specifically skip over the deletion of a .git directory in the _generated folder
                        if(containedFsInfo.Name.Equals(".git", StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        else ((DirectoryInfo)containedFsInfo).Delete(true);
                    }

                    else containedFsInfo.Delete();
                }
            }

            opTree.Data.Status = OperationStatus.Complete;
            
            foreach(var opTreeNode in opTree.Children)
            {
                ProcessFSOpTreeNode(opTreeNode, genDirFullPath);
            }
        }

        private void ProcessFSOpTreeNode(TreeNode<FSOperation> opTreeNode, string parentDirFullPath)
        {

            if(opTreeNode.Data.FSOperationType == FSOperationType.Skip || opTreeNode.Data.FSOperationType == FSOperationType.Delete)
            {
                opTreeNode.Data.Status = OperationStatus.Complete;
                return;
            }

            if(opTreeNode.Data.IsDirectoryOp)
            {
                var newParentDirFullPath = ProcessDirectoryFSOpTreeNode(opTreeNode, parentDirFullPath);
                
                foreach(var childOpTreeNode in opTreeNode.Children)
                {
                    ProcessFSOpTreeNode(childOpTreeNode, newParentDirFullPath);
                }
            }

            else
            {
                ProcessFileFSOpTreeNode(opTreeNode, parentDirFullPath);
            }
        }

        private string ProcessDirectoryFSOpTreeNode(TreeNode<FSOperation> treeNode, string parentDirFullPath)
        {
            var newDirFullPath = Path.Combine(parentDirFullPath, treeNode.Data.WorkerTask.Target);
            var newDir = Directory.CreateDirectory(newDirFullPath).FullName;
            treeNode.Data.Status = OperationStatus.Complete;
            return newDir;
        }

        private void ProcessFileFSOpTreeNode(TreeNode<FSOperation> treeNode, string parentDirFullPath)
        {
            var file = treeNode.Data.FSInfo as FileInfo;

            if(treeNode.Data.WorkerTask.TransformType == WorkType.None)
            {
                var fileName = Path.Combine(parentDirFullPath, treeNode.Data.WorkerTask.Target);
                file.CopyTo(fileName, true);
                treeNode.Data.Status = OperationStatus.Complete;
            }

            else
            {
                var fileContent = _worker.PerformTransform(treeNode.Data.WorkerTask);
                var fileName = Path.Combine(parentDirFullPath, treeNode.Data.WorkerTask.Target);

                using (var streamWriter = new StreamWriter(fileName))
                {
                    streamWriter.Write(fileContent);
                }
                
                treeNode.Data.Status = OperationStatus.Complete;
            }
        }
    }
}