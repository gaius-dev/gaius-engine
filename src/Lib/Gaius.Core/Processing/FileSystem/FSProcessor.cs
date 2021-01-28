using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Gaius.Core.Configuration;
using Gaius.Utilities.DataStructures;
using Gaius.Utilities.FileSystem;
using Gaius.Core.Worker;
using Gaius.Core.Models;

namespace Gaius.Core.Processing.FileSystem
{
    public class FSProcessor : IFSProcessor
    {
        private readonly IWorker _worker;
        private readonly GaiusConfiguration _gaiusConfiguration;

        public FSProcessor(IWorker worker, IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            _worker = worker;
            _gaiusConfiguration = gaiusConfigurationOptions.Value;
        }

        public TreeNode<FSOperation> CreateFSOperationTree()
        {
            var rootSiteDirTask = _worker.CreateWorkerTask(new DirectoryInfo(_gaiusConfiguration.SiteContainerFullPath));
            var sourceDirTask = _worker.CreateWorkerTask(new DirectoryInfo(_gaiusConfiguration.SourceDirectoryFullPath));
            var namedThemeDirTask = _worker.CreateWorkerTask(new DirectoryInfo(_gaiusConfiguration.NamedThemeDirectoryFullPath));

            var genDirectoryFullPath = _gaiusConfiguration.GenerationDirectoryFullPath;
            DirectoryInfo genDirInfo = null;

            FSOperation rootOp = null;
            FSOperation sourceDirOp = null;
            FSOperation namedThemeDirOp = null;

            rootOp = new FSOperation(rootSiteDirTask, FSOperationType.Root, _gaiusConfiguration);

            if(!Directory.Exists(genDirectoryFullPath))
            {
                 sourceDirOp = new FSOperation(sourceDirTask, FSOperationType.CreateNew, _gaiusConfiguration);
                 namedThemeDirOp = new FSOperation(namedThemeDirTask, FSOperationType.CreateNew, _gaiusConfiguration);
            }
                
            else
            {
                sourceDirOp = new FSOperation(sourceDirTask, FSOperationType.Overwrite, _gaiusConfiguration);
                namedThemeDirOp = new FSOperation(namedThemeDirTask, FSOperationType.Overwrite, _gaiusConfiguration);
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
            AddOperationsToTreeNode(sourceDirTreeNode, sourceDirTask.DirectoryInfo, genDirInfo);

            var namedThemeDirTreeNode = opTree.AddChild(namedThemeDirOp);
            AddOperationsToTreeNode(namedThemeDirTreeNode, namedThemeDirTask.DirectoryInfo, genDirInfo);

            AddAdditionalPostListingPaginatorOps(sourceDirTreeNode);

            RemoveFalsePositiveDeleteOps(sourceDirTreeNode, namedThemeDirTreeNode);

            return opTree;            
        }

        private void AddOperationsToTreeNode(TreeNode<FSOperation> startNode, DirectoryInfo sourceStartDir, DirectoryInfo genStartDir)
        {
            // Files ==========================================================
            var matchingGenFiles = new List<FileInfo>();

            foreach(var sourceFile in sourceStartDir?.EnumerateFiles().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<FileInfo>())
            {
                var hasMatch = false;
                var matchingGenFile = FindMachingGenerationFile(genStartDir, sourceFile);

                //rs: we have a source file with a matching file in the generation directory
                if(matchingGenFile != null)
                {
                    hasMatch = true;
                    matchingGenFiles.Add(matchingGenFile);
                }

                var sourceFileTask = _worker.CreateWorkerTask(sourceFile);

                if(hasMatch && sourceFileTask.ShouldSkipKeep)
                {
                    startNode.AddChild(new FSOperation(sourceFileTask, FSOperationType.Skip, _gaiusConfiguration));
                    startNode.AddChild(new FSOperation(sourceFileTask, FSOperationType.Keep, _gaiusConfiguration));
                }

                else if(hasMatch && sourceFileTask.ShouldSkip)
                    startNode.AddChild(new FSOperation(sourceFileTask, FSOperationType.SkipDelete, _gaiusConfiguration));

                else if(hasMatch && sourceFileTask.IsDraft)
                    startNode.AddChild(new FSOperation(sourceFileTask, FSOperationType.SkipDraft, _gaiusConfiguration));

                else if(hasMatch)
                    startNode.AddChild(new FSOperation(sourceFileTask, FSOperationType.Overwrite, _gaiusConfiguration));

                else if(!hasMatch && sourceFileTask.ShouldSkip)
                    startNode.AddChild(new FSOperation(sourceFileTask, FSOperationType.Skip, _gaiusConfiguration));

                else if(!hasMatch && sourceFileTask.IsDraft)
                    startNode.AddChild(new FSOperation(sourceFileTask, FSOperationType.SkipDraft, _gaiusConfiguration));

                else startNode.AddChild(new FSOperation(sourceFileTask, FSOperationType.CreateNew, _gaiusConfiguration));
            }

            //rs: all other files in the generation dir are considered orphaned
            foreach(var orphanGenFile in genStartDir?.EnumerateFiles()
                .Where(file => !matchingGenFiles.Any(exFile => exFile.Name.Equals(file.Name))) ?? Enumerable.Empty<FileInfo>())
            {
                var orphanGenFileTask = _worker.CreateWorkerTask(orphanGenFile);

                //rs: is this a special file that should be kept despite being orphaned?
                if(orphanGenFileTask.ShouldKeep)
                    startNode.AddChild(new FSOperation(orphanGenFileTask, FSOperationType.Keep, _gaiusConfiguration));

                else startNode.AddChild(new FSOperation(orphanGenFileTask, FSOperationType.Delete, _gaiusConfiguration));
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

                var sourceDirTask = _worker.CreateWorkerTask(sourceDir);

                if(hasMatch && sourceDirTask.ShouldSkipKeep)
                {
                    startNode.AddChild(new FSOperation(sourceDirTask, FSOperationType.Skip, _gaiusConfiguration));
                    startNode.AddChild(new FSOperation(sourceDirTask, FSOperationType.Keep, _gaiusConfiguration));
                }

                else if(hasMatch && sourceDirTask.ShouldSkip)
                    newOpTreeNode = startNode.AddChild(new FSOperation(sourceDirTask, FSOperationType.SkipDelete, _gaiusConfiguration));

                else if(hasMatch)
                    newOpTreeNode = startNode.AddChild(new FSOperation(sourceDirTask, FSOperationType.Overwrite, _gaiusConfiguration));
                
                else if(!hasMatch && sourceDirTask.ShouldSkip)
                    newOpTreeNode = startNode.AddChild(new FSOperation(sourceDirTask, FSOperationType.Skip, _gaiusConfiguration));

                else newOpTreeNode = startNode.AddChild(new FSOperation(sourceDirTask, FSOperationType.CreateNew, _gaiusConfiguration));

                if(newOpTreeNode != null)
                    AddOperationsToTreeNode(newOpTreeNode, sourceDir, matchingGenDir);
            }

            foreach(var orphanGenDir in genStartDir?.EnumerateDirectories()
                .Where(dir => !matchingGenDirs.Any(exDir=> exDir.Name.Equals(dir.Name))) ?? Enumerable.Empty<DirectoryInfo>())
            {
                var orphanGenDirTask = _worker.CreateWorkerTask(orphanGenDir);

                //rs: is this a special dir that should be kept despite being orphaned?
                if(orphanGenDirTask.ShouldKeep)
                    startNode.AddChild(new FSOperation(orphanGenDirTask, FSOperationType.Keep, _gaiusConfiguration));

                else startNode.AddChild(new FSOperation(orphanGenDirTask, FSOperationType.Delete, _gaiusConfiguration));
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

        private static void RemoveFalsePositiveDeleteOps(TreeNode<FSOperation> sourceTreeNode, TreeNode<FSOperation> namedThemeTreeNode)
        {
            var invalidDeleteOpTreeNodes = new List<TreeNode<FSOperation>>();

            foreach(var sourceTreeNodeDeleteOp in sourceTreeNode.Where(tn => tn.Data.FSOperationType == FSOperationType.Delete))
            {
                //rs: we have another Op in the named theme tree node that renders the delete Op in the source tree node invalid
                if(namedThemeTreeNode.Any(ntTn => 
                    (ntTn.Data.WorkerTask != null && ntTn.Data.WorkerTask.TargetFSName == sourceTreeNodeDeleteOp.Data.Name)
                    && ntTn.Level == sourceTreeNodeDeleteOp.Level
                    && (ntTn.Data.FSOperationType == FSOperationType.CreateNew || ntTn.Data.FSOperationType == FSOperationType.Overwrite)))
                    {
                        invalidDeleteOpTreeNodes.Add(sourceTreeNodeDeleteOp);
                    }
            }

            foreach(var invalidTreeNode in invalidDeleteOpTreeNodes)
            {
                if(invalidTreeNode.Parent != null)
                    invalidTreeNode.Parent.Children.Remove(invalidTreeNode);
            }

            invalidDeleteOpTreeNodes.Clear();

            foreach(var namedThemeDeleteOp in namedThemeTreeNode.Where(tn => tn.Data.FSOperationType == FSOperationType.Delete))
            {
                //rs: we have another Op in the source tree node that renders the delete Op in the named theme tree node invalid
                if(sourceTreeNode.Any(srcTn =>
                    (srcTn.Data.WorkerTask != null && srcTn.Data.WorkerTask.TargetFSName == namedThemeDeleteOp.Data.Name)
                    && srcTn.Level == namedThemeDeleteOp.Level
                    && (srcTn.Data.FSOperationType == FSOperationType.CreateNew || srcTn.Data.FSOperationType == FSOperationType.Overwrite)))
                    {
                        invalidDeleteOpTreeNodes.Add(namedThemeDeleteOp);
                    }
            }

            foreach(var invalidTreeNode in invalidDeleteOpTreeNodes)
            {
                if(invalidTreeNode.Parent != null)
                    invalidTreeNode.Parent.Children.Remove(invalidTreeNode);
            }
        }

        private void AddAdditionalPostListingPaginatorOps(TreeNode<FSOperation> sourceTreeNode)
        {
            //rs: get all post ops worker tasks
            var allPostWorkerTasks = sourceTreeNode.Where(tn => tn.Data.WorkerTask.IsPost).Select(tn => tn.Data.WorkerTask).ToList();

            var allDefaultPostListingsNodes = sourceTreeNode.Where(tn => tn.Data.IsListingOp && tn.Data.WorkerTask.IsDefaultPostListing).ToList();

            //rs: add additional paginator ops for any default post listing pages
            foreach(var defaultPostListingNode in allDefaultPostListingsNodes)
            {
                AddAdditionalPostListingPaginatorOpsForId(defaultPostListingNode, "posts", allPostWorkerTasks);
            }
        }

        private void AddAdditionalPostListingPaginatorOpsForId(TreeNode<FSOperation> pageListTreeNode, string paginatorId, List<WorkerTask> postWorkerTasks)
        {
            if(postWorkerTasks.Count == 0)
                return;

            var itemsPerPage = _gaiusConfiguration.Pagination;
            var totalItems = postWorkerTasks.Count;
            var totalPages = totalItems / itemsPerPage;

            if(totalItems % itemsPerPage > 0)
                totalPages++;

            var firstPagePostWorkerTasks = postWorkerTasks.Take(itemsPerPage).ToList();
            var firstPagePaginatorData = new PaginatorData(paginatorId, itemsPerPage, 1, totalPages, totalItems);
            _worker.AddPaginatorDataToWorkerTask(pageListTreeNode.Data.WorkerTask, firstPagePaginatorData, firstPagePostWorkerTasks);

            for(var pg = 2; pg <= totalPages; pg++)
            {
                var pgPostWorkerTasks = postWorkerTasks.Skip((pg - 1) * itemsPerPage).Take(itemsPerPage).ToList();
                var pgPaginatorData = new PaginatorData(paginatorId, itemsPerPage, pg, totalPages, totalItems);
                var additionalWorkerTask = _worker.CreateWorkerTask(pageListTreeNode.Data.WorkerTask.FileSystemInfo, pgPaginatorData, pgPostWorkerTasks);
                var additonalOp = new FSOperation(additionalWorkerTask, FSOperationType.AdditionalListingPage, _gaiusConfiguration);
                pageListTreeNode.AddChild(additonalOp);
            }
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
                if(_worker.GetShouldKeep(containedFsInfo))
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

            //rs: nothing to do for this particular op
            if(opTreeNode.Data.IsWorkerOmittedForOp)
            {
                opTreeNode.Data.Status = OperationStatus.Complete;
                return;
            }

            //rs: this is a directory op, process it, then each of it's child file ops
            else if(opTreeNode.Data.IsDirectoryOp)
            {
                var newParentDirFullPath = ProcessDirectoryFSOpTreeNode(opTreeNode, parentDirFullPath);
                
                foreach(var childOpTreeNode in opTreeNode.Children)
                {
                    ProcessFSOpTreeNode(childOpTreeNode, newParentDirFullPath);
                }
            }

            //rs: this is a file listing op, process it, then each of it's children (in the same parent directory)
            else if(opTreeNode.Data.IsListingOp)
            {
                ProcessFileFSOpTreeNode(opTreeNode, parentDirFullPath);

                foreach(var childOpTreeNode in opTreeNode.Children)
                {
                    ProcessFileFSOpTreeNode(childOpTreeNode, parentDirFullPath);
                }
            }

            //rs: this is a regular file op, process it
            else ProcessFileFSOpTreeNode(opTreeNode, parentDirFullPath);
        }

        private string ProcessDirectoryFSOpTreeNode(TreeNode<FSOperation> treeNode, string parentDirFullPath)
        {
            var newDirFullPath = Path.Combine(parentDirFullPath, treeNode.Data.WorkerTask.TargetFSName);
            var newDir = Directory.CreateDirectory(newDirFullPath).FullName;
            treeNode.Data.Status = OperationStatus.Complete;
            return newDir;
        }

        private void ProcessFileFSOpTreeNode(TreeNode<FSOperation> treeNode, string parentDirFullPath)
        {
            var file = treeNode.Data.WorkerTask.FileInfo;

            if(treeNode.Data.WorkerTask.WorkType == WorkType.None)
            {
                var fileName = Path.Combine(parentDirFullPath, treeNode.Data.WorkerTask.TargetFSName);
                file.CopyTo(fileName, true);
                treeNode.Data.Status = OperationStatus.Complete;
            }

            else
            {
                var fileContent = _worker.PerformWork(treeNode.Data.WorkerTask);
                var fileName = Path.Combine(parentDirFullPath, treeNode.Data.WorkerTask.TargetFSName);

                using (var streamWriter = new StreamWriter(fileName))
                {
                    streamWriter.Write(fileContent);
                }
                
                treeNode.Data.Status = OperationStatus.Complete;
            }
        }
    }
}