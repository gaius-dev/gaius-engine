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

        public TreeNode<FSOperation> CreateFSOperationTree()
        {
            var rootSiteDirInfo = new DirectoryInfo(_gaiusConfiguration.SiteContainerFullPath);
            var sourceDirInfo = new DirectoryInfo(_gaiusConfiguration.SourceDirectoryFullPath);
            var namedThemeDirInfo = new DirectoryInfo(_gaiusConfiguration.NamedThemeDirectoryFullPath);

            var genDirectoryFullPath = _gaiusConfiguration.GenerationDirectoryFullPath;
            DirectoryInfo genDirInfo = null;

            FSOperation rootOp = null;
            FSOperation sourceDirOp = null;
            FSOperation namedThemeDirOp = null;

            rootOp = FSOperation.CreateInstance(_provider, rootSiteDirInfo, FSOperationType.Root);

            if(!Directory.Exists(genDirectoryFullPath))
            {
                 sourceDirOp = FSOperation.CreateInstance(_provider, sourceDirInfo, FSOperationType.CreateNew);
                 namedThemeDirOp = FSOperation.CreateInstance(_provider, namedThemeDirInfo, FSOperationType.CreateNew);
            }
                
            else
            {
                sourceDirOp = FSOperation.CreateInstance(_provider, sourceDirInfo, FSOperationType.Overwrite);
                namedThemeDirOp = FSOperation.CreateInstance(_provider, namedThemeDirInfo, FSOperationType.Overwrite, $"{_gaiusConfiguration.ThemesDirectoryName}/{namedThemeDirInfo.Name}");
                genDirInfo = new DirectoryInfo(_gaiusConfiguration.GenerationDirectoryFullPath);
            }

            /*====== FS Operation Tree Structure ======||
            ||                                         ||
            ||     Site Container Dir (Root)           ||
            ||         //           \\                 ||
            ||        //             \\                ||
            || [_src -> _gen]   [named theme -> _gen]  ||
            ||      //                 \\              ||
            ||     //                   \\             ||
            ||  children              children         ||
            ||=========================================*/

            var opTree = new TreeNode<FSOperation>(rootOp);

            var sourceDirTreeNode = opTree.AddChild(sourceDirOp);
            AddOperationsToTreeNode(sourceDirTreeNode, sourceDirInfo, genDirInfo);

            var namedThemeDirTreeNode = opTree.AddChild(namedThemeDirOp);
            AddOperationsToTreeNode(namedThemeDirTreeNode, namedThemeDirInfo, genDirInfo);

            return opTree;            
        }

        private void AddOperationsToTreeNode(TreeNode<FSOperation> startNode, DirectoryInfo sourceStartDir, DirectoryInfo genStartDir)
        {
            // Files ==========================================================
            var matchingGenFiles = new List<FileInfo>();

            foreach(var sourceFile in sourceStartDir?.EnumerateFiles() ?? Enumerable.Empty<FileInfo>())
            {
                var hasMatch = false;
                var matchingGenFile = FindMachingGenerationFile(genStartDir, sourceFile);

                //rs: we have a source file with a matching file in the generation directory
                if(matchingGenFile != null)
                {
                    hasMatch = true;
                    matchingGenFiles.Add(matchingGenFile);
                }

                if(hasMatch && _worker.ShouldSkip(sourceFile) && _worker.ShouldKeep(sourceFile))
                {
                    startNode.AddChild(FSOperation.CreateInstance(_provider, sourceFile, FSOperationType.Skip));
                    startNode.AddChild(FSOperation.CreateInstance(_provider, sourceFile, FSOperationType.Keep));
                }

                else if(hasMatch && _worker.ShouldSkip(sourceFile))
                    startNode.AddChild(FSOperation.CreateInstance(_provider, sourceFile, FSOperationType.SkipDelete));

                else if(hasMatch)
                    startNode.AddChild(FSOperation.CreateInstance(_provider, sourceFile, FSOperationType.Overwrite));

                else if(!hasMatch && _worker.ShouldSkip(sourceFile))
                    startNode.AddChild(FSOperation.CreateInstance(_provider, sourceFile, FSOperationType.Skip));

                else startNode.AddChild(FSOperation.CreateInstance(_provider, sourceFile, FSOperationType.CreateNew));
            }

            //rs: all other files in the generation dir are considered orphaned
            foreach(var orphanGenFile in genStartDir?.EnumerateFiles()
                .Where(file => !matchingGenFiles.Any(exFile => exFile.Name.Equals(file.Name))) ?? Enumerable.Empty<FileInfo>())
            {
                //rs: is this a special file that should be kept despite being orphaned?
                if(_worker.ShouldKeep(orphanGenFile))
                    startNode.AddChild(FSOperation.CreateInstance(_provider, orphanGenFile, FSOperationType.Keep));

                else startNode.AddChild(FSOperation.CreateInstance(_provider, orphanGenFile, FSOperationType.Delete));
            }

            // Directories ====================================================
            var matchingGenDirs = new List<DirectoryInfo>();

            foreach(var sourceDir in sourceStartDir?.EnumerateDirectories() ?? Enumerable.Empty<DirectoryInfo>())
            {
                var hasMatch = false;
                var matchingGenDir = FindMatchingGenerationDirectory(genStartDir, sourceDir);

                //rs: we have a source directory with a matching directory in the generation directory
                if(matchingGenDir != null)
                {
                    hasMatch = true;
                    matchingGenDirs.Add(matchingGenDir);
                }

                TreeNode<FSOperation> newOpTreeNode = null;

                if(hasMatch && _worker.ShouldSkip(sourceDir) && _worker.ShouldKeep(sourceDir))
                {
                    startNode.AddChild(FSOperation.CreateInstance(_provider, sourceDir, FSOperationType.Skip));
                    startNode.AddChild(FSOperation.CreateInstance(_provider, sourceDir, FSOperationType.Keep));
                }

                else if(hasMatch && _worker.ShouldSkip(sourceDir))
                    newOpTreeNode = startNode.AddChild(FSOperation.CreateInstance(_provider, sourceDir, FSOperationType.SkipDelete));

                else if(hasMatch)
                    newOpTreeNode = startNode.AddChild(FSOperation.CreateInstance(_provider, sourceDir, FSOperationType.Overwrite));
                
                else if(!hasMatch && _worker.ShouldSkip(sourceDir))
                    newOpTreeNode = startNode.AddChild(FSOperation.CreateInstance(_provider, sourceDir, FSOperationType.Skip));

                else newOpTreeNode = startNode.AddChild(FSOperation.CreateInstance(_provider, sourceDir, FSOperationType.CreateNew));

                if(newOpTreeNode != null)
                    AddOperationsToTreeNode(newOpTreeNode, sourceDir, matchingGenDir);
            }

            foreach(var orphanGenDir in genStartDir?.EnumerateDirectories()
                .Where(dir => !matchingGenDirs.Any(exDir=> exDir.Name.Equals(dir.Name))) ?? Enumerable.Empty<DirectoryInfo>())
            {
                //rs: is this a special dir that should be kept despite being orphaned?
                if(_worker.ShouldKeep(orphanGenDir))
                    startNode.AddChild(FSOperation.CreateInstance(_provider, orphanGenDir, FSOperationType.Keep));

                else startNode.AddChild(FSOperation.CreateInstance(_provider, orphanGenDir, FSOperationType.Delete));
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
            var siteDirFullPath = _gaiusConfiguration.SiteContainerFullPath;
            var genDirFullPath = _gaiusConfiguration.GenerationDirectoryFullPath;

            if(!Directory.Exists(genDirFullPath))
            {
                Directory.CreateDirectory(genDirFullPath);
            }

            var genDir = new DirectoryInfo(genDirFullPath);
            var allContainedFsInfos = genDir.EnumerateFileSystemInfos();

            //rs: delete *almost* all contained directories and files in the generation directory
            foreach(var containedFsInfo in allContainedFsInfos)
            {
                if(_worker.ShouldKeep(containedFsInfo))
                    continue;

                if(containedFsInfo.IsDirectory())
                    ((DirectoryInfo)containedFsInfo).Delete(true);

                else containedFsInfo.Delete();
            }
            
            opTree.Data.Status = OperationStatus.Complete;
            
            foreach(var opTreeNode in opTree.Children)
            {
                ProcessFSOpTreeNode(opTreeNode, siteDirFullPath);
            }
        }

        private void ProcessFSOpTreeNode(TreeNode<FSOperation> opTreeNode, string parentDirFullPath)
        {

            if(opTreeNode.Data.IsWorkerOmittedForOp)
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