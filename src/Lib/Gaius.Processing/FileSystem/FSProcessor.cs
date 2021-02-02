using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Gaius.Core.Configuration;
using Gaius.Core.DataStructures;
using Gaius.Core.FileSystem;
using Gaius.Worker;
using Gaius.Worker.Models;

namespace Gaius.Processing.FileSystem
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

        public TreeNode<IOperation> CreateFSOperationTree()
        {
            var rootSiteDirTask = _worker.CreateWorkerTask(new DirectoryInfo(_gaiusConfiguration.SiteContainerFullPath));
            var sourceDirTask = _worker.CreateWorkerTask(new DirectoryInfo(_gaiusConfiguration.SourceDirectoryFullPath));
            var namedThemeDirTask = _worker.CreateWorkerTask(new DirectoryInfo(_gaiusConfiguration.NamedThemeDirectoryFullPath));

            FSOperation rootOp = null;
            FSOperation sourceDirOp = null;
            FSOperation namedThemeDirOp = null;

            rootOp = new FSOperation(rootSiteDirTask, OperationType.Root);
            sourceDirOp = new FSOperation(sourceDirTask);
            namedThemeDirOp = new FSOperation(namedThemeDirTask);

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

            var rootNode = new TreeNode<IOperation>(rootOp);

            var sourceDirTreeNode = rootNode.AddChild(sourceDirOp);
            AddSourceOperationsToTreeNode(sourceDirTreeNode, sourceDirTask.DirectoryInfo);

            var namedThemeDirTreeNode = rootNode.AddChild(namedThemeDirOp);
            AddSourceOperationsToTreeNode(namedThemeDirTreeNode, namedThemeDirTask.DirectoryInfo);

            AddAdditionalPostListingPaginatorOps(sourceDirTreeNode);

            var genDirectoryInfo = new DirectoryInfo(_gaiusConfiguration.GenerationDirectoryFullPath);
            AddGenerationDirOperationsToTreeNode(rootNode, rootNode, genDirectoryInfo);

            PruneNullGenDirOpNodes(rootNode);

            return rootNode;            
        }

        private void AddSourceOperationsToTreeNode(TreeNode<IOperation> startNode, DirectoryInfo sourceStartDir)
        {
            // Files ==========================================================
            foreach(var sourceFile in sourceStartDir?.EnumerateFiles().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<FileInfo>())
            {
                var sourceFileTask = _worker.CreateWorkerTask(sourceFile);
                var sourceFileOp = new FSOperation(sourceFileTask);
                startNode.AddChild(sourceFileOp);
            }

            // Directories ====================================================
            foreach(var sourceDir in sourceStartDir?.EnumerateDirectories().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<DirectoryInfo>())
            {
                var sourceDirTask = _worker.CreateWorkerTask(sourceDir);
                var sourceDirOp = new FSOperation(sourceDirTask);
                var newOpTreeNode = startNode.AddChild(sourceDirOp);
                AddSourceOperationsToTreeNode(newOpTreeNode, sourceDir);
            }
        }

        private void AddAdditionalPostListingPaginatorOps(TreeNode<IOperation> sourceTreeNode)
        {
            //rs: get all post ops worker tasks
            var allPostWorkerTasks = sourceTreeNode.Where(tn 
                => !tn.Data.IsInvalid
                    && tn.Data.WorkerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsPost))
                        .Select(tn => tn.Data.WorkerTask).ToList();

            var allPostListOpNodes = sourceTreeNode.Where(tn => tn.Data.WorkerTask.IsPostListing).ToList();

            //rs: add additional paginator ops for any default post listing pages
            foreach(var postListOpNode in allPostListOpNodes)
            {
                AddAdditionalPostListingPaginatorOpsForId(postListOpNode, allPostWorkerTasks);
            }
        }

        private void AddAdditionalPostListingPaginatorOpsForId(TreeNode<IOperation> postListOpNode, List<WorkerTask> postWorkerTasks)
        {
            if(postWorkerTasks.Count == 0)
                return;

            var itemsPerPage = _gaiusConfiguration.Pagination;
            var totalItems = postWorkerTasks.Count;
            var totalPages = totalItems / itemsPerPage;

            if(totalItems % itemsPerPage > 0)
                totalPages++;

            var firstPagePostWorkerTasks = postWorkerTasks.Take(itemsPerPage).ToList();
            var firstPagePaginatorData = new Paginator(itemsPerPage, 1, totalPages, totalItems);
            _worker.AddPaginatorDataToWorkerTask(postListOpNode.Data.WorkerTask, firstPagePaginatorData, firstPagePostWorkerTasks);

            for(var pg = 2; pg <= totalPages; pg++)
            {
                var pgPostWorkerTasks = postWorkerTasks.Skip((pg - 1) * itemsPerPage).Take(itemsPerPage).ToList();
                var pgPaginatorData = new Paginator(itemsPerPage, pg, totalPages, totalItems);
                var additionalWorkerTask = _worker.CreateWorkerTask(postListOpNode.Data.WorkerTask.FileSystemInfo, pgPaginatorData, pgPostWorkerTasks);
                var additonalOp = new FSOperation(additionalWorkerTask);
                postListOpNode.AddChild(additonalOp);
            }
        }

        private void AddGenerationDirOperationsToTreeNode(TreeNode<IOperation> rootNode, TreeNode<IOperation> startNode, DirectoryInfo generationStartDir)
        {
            if(!generationStartDir.Exists)
                return;
                
            // Files ==========================================================
            foreach(var genFile in generationStartDir?.EnumerateFiles().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<FileInfo>())
            {
                if(!rootNode.Any(node => !node.Data.IsNullOp && node.Data.WorkerTask.TaskFullPath == genFile.FullName))
                {
                    var genFileTask = _worker.CreateWorkerTask(genFile);
                    var genFileOp = new FSOperation(genFileTask);
                    startNode.AddChild(genFileOp);
                }
            }

            // Directories ====================================================
            foreach(var genDir in generationStartDir?.EnumerateDirectories().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<DirectoryInfo>())
            {
                //rs: does the gen dir op have a matching source dir op?
                var hasMatchingSourceDirOp = rootNode.Any(node => !node.Data.IsNullOp && node.Data.WorkerTask.TaskFullPath == genDir.FullName);

                //rs: check a special condition first
                // 1. We don't have an explicit matching source dir op
                // 2. AND we don't have a source file op which contains this gen directory (e.g. post and draft file ops)
                // This means we'll need to either delet or keep this generation directory
                if(!hasMatchingSourceDirOp && !rootNode.Any(node => !node.Data.IsNullOp && node.Data.WorkerTask.TaskParentDirectory.Contains(genDir.FullName)))
                {
                    var genDirTask = _worker.CreateWorkerTask(genDir);
                    var genDirOp = new FSOperation(genDirTask);
                    var newGenDirOpNode = startNode.AddChild(genDirOp);
                    AddGenerationDirOperationsToTreeNode(rootNode, newGenDirOpNode, genDir);
                }

                //rs: we do have a matching dir op, just skip this gen dir and add nothing to the op tree
                else if(hasMatchingSourceDirOp)
                    continue;

                //rs: we don't really know if we'll need this gen dir op or not, add it as a null op
                //NOTE: this could be pruned later if it doesn't contain any child op nodes that are deletes or keeps
                else
                {
                    var nullGenDirOp = new FSOperation(genDir.Name, genDir.Name);
                    var newNullGenDirOpNode = startNode.AddChild(nullGenDirOp);
                    AddGenerationDirOperationsToTreeNode(rootNode, newNullGenDirOpNode, genDir);
                }
            }
        }

        private void PruneNullGenDirOpNodes(TreeNode<IOperation> rootNode)
        {
            var level1NullGenDirOps = rootNode.Where(node => node.Data.IsNullOp).ToList();

            foreach(var nullGenDirOp in level1NullGenDirOps)
            {
                //rs: this is an op that's already been pruned, just skip it
                if(nullGenDirOp.Parent == null)
                    continue;

                //rs: the null op has no delete or keep children underneath it, no reason to keep it
                if(!nullGenDirOp.Any(node 
                    => node.Data.OperationType == OperationType.Delete
                        || node.Data.OperationType == OperationType.Keep))
                    nullGenDirOp.Parent.Children.Remove(nullGenDirOp);
            }
        }

        public void ProcessFSOperationTree(TreeNode<IOperation> rootNode)
        {
            var siteDirFullPath = _gaiusConfiguration.SiteContainerFullPath;
            var genDirFullPath = _gaiusConfiguration.GenerationDirectoryFullPath;

            if(!Directory.Exists(genDirFullPath))
                Directory.CreateDirectory(genDirFullPath);

            var genDir = new DirectoryInfo(genDirFullPath);
            var allContainedFsInfos = genDir.EnumerateFileSystemInfos();

            //rs: delete *almost* all contained directories and files in the generation directory
            foreach(var containedFsInfo in allContainedFsInfos)
            {
                //rs: we have an explicit request to keep a specific file or directory
                if(rootNode.Any(node => !node.Data.IsNullOp && node.Data.WorkerTask.TaskFullPath == containedFsInfo.FullName
                    && node.Data.OperationType == OperationType.Keep))
                    continue;

                if(containedFsInfo.IsDirectory())
                    ((DirectoryInfo)containedFsInfo).Delete(true);

                else containedFsInfo.Delete();
            }
            
            rootNode.Data.Status = OperationStatus.Complete;
            
            foreach(var opNode in rootNode)
            {
                ProcessFSOpTreeNode(opNode);
            }
        }

        private void ProcessFSOpTreeNode(TreeNode<IOperation> opNode)
        {
            //rs: nothing to do for this particular op
            if(opNode.Data.NoActionRequired)
            {
                opNode.Data.Status = OperationStatus.Complete;
                return;
            }

            else if(opNode.Data.IsDirectoryOp)
            {
                var newDir = Directory.CreateDirectory(opNode.Data.WorkerTask.TaskFullPath);
                opNode.Data.Status = newDir != null ? OperationStatus.Complete : OperationStatus.Error;
                return;
            }

            //rs: this is a regular file op, process it
            ProcessFileFSOpTreeNode(opNode);
        }

        private void ProcessFileFSOpTreeNode(TreeNode<IOperation> opNode)
        {
            Directory.CreateDirectory(opNode.Data.WorkerTask.TaskParentDirectory);

            if(opNode.Data.WorkerTask.WorkType == WorkType.None)
            {
                opNode.Data.WorkerTask.FileInfo.CopyTo(opNode.Data.WorkerTask.TaskFullPath, true);
                opNode.Data.Status = OperationStatus.Complete;
                return;
            }

            var fileContent = _worker.PerformWork(opNode.Data.WorkerTask);

            using (var streamWriter = new StreamWriter(opNode.Data.WorkerTask.TaskFullPath))
            {
                streamWriter.Write(fileContent);
            }
            
            opNode.Data.Status = OperationStatus.Complete;
        }
    }
}