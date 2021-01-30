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

            FSOperation rootOp = null;
            FSOperation sourceDirOp = null;
            FSOperation namedThemeDirOp = null;

            rootOp = new FSOperation(rootSiteDirTask, FSOperationType.Root);
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

            var rootNode = new TreeNode<FSOperation>(rootOp);

            var sourceDirTreeNode = rootNode.AddChild(sourceDirOp);
            AddSourceOperationsToTreeNode(sourceDirTreeNode, sourceDirTask.DirectoryInfo);

            var namedThemeDirTreeNode = rootNode.AddChild(namedThemeDirOp);
            AddSourceOperationsToTreeNode(namedThemeDirTreeNode, namedThemeDirTask.DirectoryInfo);

            AddAdditionalPostListingPaginatorOps(sourceDirTreeNode);
            UpdatedOperationTypes(rootNode);

            var genDirectoryInfo = new DirectoryInfo(_gaiusConfiguration.GenerationDirectoryFullPath);
            AddGenerationDirOperationsToTreeNode(rootNode, rootNode, genDirectoryInfo);

            return rootNode;            
        }

        private void AddSourceOperationsToTreeNode(TreeNode<FSOperation> startNode, DirectoryInfo sourceStartDir)
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
                TreeNode<FSOperation> newOpTreeNode = null;

                var sourceDirTask = _worker.CreateWorkerTask(sourceDir);
                var sourceDirOp = new FSOperation(sourceDirTask);
                newOpTreeNode = startNode.AddChild(sourceDirOp);

                if(newOpTreeNode != null)
                    AddSourceOperationsToTreeNode(newOpTreeNode, sourceDir);
            }
        }

        private void UpdatedOperationTypes(TreeNode<FSOperation> rootNode)
        {
            foreach(var sourceOpNode in rootNode.Where(node => node.Data.FSOperationType == FSOperationType.Undefined))
            {
                if(sourceOpNode.Data.IsDirectoryOp)
                    sourceOpNode.Data.FSOperationType = Directory.Exists(sourceOpNode.Data.WorkerTask.TargetFullPath)
                        ? FSOperationType.Overwrite
                        : FSOperationType.CreateNew;

                else sourceOpNode.Data.FSOperationType = File.Exists(sourceOpNode.Data.WorkerTask.TargetFullPath)
                        ? FSOperationType.Overwrite
                        : FSOperationType.CreateNew;
            }
        }

        private void AddAdditionalPostListingPaginatorOps(TreeNode<FSOperation> sourceTreeNode)
        {
            //rs: get all post ops worker tasks
            var allPostWorkerTasks = sourceTreeNode.Where(tn => tn.Data.WorkerTask.IsPost).Select(tn => tn.Data.WorkerTask).ToList();

            var allPostListOpNodes = sourceTreeNode.Where(tn => tn.Data.WorkerTask.IsPostListing).ToList();

            //rs: add additional paginator ops for any default post listing pages
            foreach(var postListOpNode in allPostListOpNodes)
            {
                AddAdditionalPostListingPaginatorOpsForId(postListOpNode, allPostWorkerTasks);
            }
        }

        private void AddAdditionalPostListingPaginatorOpsForId(TreeNode<FSOperation> postListOpNode, List<WorkerTask> postWorkerTasks)
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

        private void AddGenerationDirOperationsToTreeNode(TreeNode<FSOperation> rootNode, TreeNode<FSOperation> startNode, DirectoryInfo generationStartDir)
        {
            // Files ==========================================================
            foreach(var genFile in generationStartDir?.EnumerateFiles().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<FileInfo>())
            {
                if(!rootNode.Any(node => node.Data.WorkerTask.TargetFullPath == genFile.FullName))
                {
                    var genFileTask = _worker.CreateWorkerTask(genFile);
                    var genFileOp = new FSOperation(genFileTask);

                    if(genFileOp.FSOperationType == FSOperationType.Undefined)
                        genFileOp.FSOperationType = FSOperationType.Delete;
                    
                    startNode.AddChild(genFileOp);
                }
            }

            // Directories ====================================================
            foreach(var genDir in generationStartDir?.EnumerateDirectories().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<DirectoryInfo>())
            {
                if(!rootNode.Any(node => node.Data.WorkerTask.TargetFullPath == genDir.FullName))
                {
                    TreeNode<FSOperation> newOpTreeNode = null;

                    var genDirTask = _worker.CreateWorkerTask(genDir);
                    var genDirOp = new FSOperation(genDirTask);

                    if(genDirOp.FSOperationType == FSOperationType.Undefined)
                        genDirOp.FSOperationType = FSOperationType.Delete;

                    newOpTreeNode = startNode.AddChild(genDirOp);

                    if(newOpTreeNode != null)
                        AddGenerationDirOperationsToTreeNode(rootNode, newOpTreeNode, genDir);
                }
            }
        }

        public void ProcessFSOperationTree(TreeNode<FSOperation> rootNode)
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
                if(rootNode.Any(node => node.Data.WorkerTask.TargetFullPath == containedFsInfo.FullName
                    && node.Data.FSOperationType == FSOperationType.Keep))
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

        private void ProcessFSOpTreeNode(TreeNode<FSOperation> opNode)
        {

            //rs: nothing to do for this particular op
            if(opNode.Data.IsEmptyOp)
            {
                opNode.Data.Status = OperationStatus.Complete;
                return;
            }

            else if(opNode.Data.IsDirectoryOp)
            {
                var newDir = Directory.CreateDirectory(opNode.Data.WorkerTask.TargetFullPath);
                opNode.Data.Status = newDir != null ? OperationStatus.Complete : OperationStatus.Error;
                return;
            }

            //rs: this is a regular file op, process it
            ProcessFileFSOpTreeNode(opNode);
        }

        private void ProcessFileFSOpTreeNode(TreeNode<FSOperation> opNode)
        {
            Directory.CreateDirectory(opNode.Data.WorkerTask.TargetParentDirectory);

            if(opNode.Data.WorkerTask.WorkType == WorkType.Copy)
            {
                opNode.Data.WorkerTask.FileInfo.CopyTo(opNode.Data.WorkerTask.TargetFullPath, true);
                opNode.Data.Status = OperationStatus.Complete;
                return;
            }

            var fileContent = _worker.PerformWork(opNode.Data.WorkerTask);

            using (var streamWriter = new StreamWriter(opNode.Data.WorkerTask.TargetFullPath))
            {
                streamWriter.Write(fileContent);
            }
            
            opNode.Data.Status = OperationStatus.Complete;
        }
    }
}