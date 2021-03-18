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

            var rootTreeNode = new TreeNode<IOperation>(rootOp);

            var sourceDirTreeNode = rootTreeNode.AddChild(sourceDirOp);
            AddSourceOperationsToTreeNode(sourceDirTreeNode, sourceDirTask.DirectoryInfo);
            AddBaseNavDataToWorker(sourceDirTreeNode, false);
            AddBaseNavDataToWorker(sourceDirTreeNode, true);
            AddAdditionalPostListingPaginatorOps(sourceDirTreeNode);
            AddTagDataToWorkerAndAdditionalTagListingOps(sourceDirTreeNode);
            
            var namedThemeDirTreeNode = rootTreeNode.AddChild(namedThemeDirOp);
            AddSourceOperationsToTreeNode(namedThemeDirTreeNode, namedThemeDirTask.DirectoryInfo);

            return rootTreeNode;            
        }

        private void AddSourceOperationsToTreeNode(TreeNode<IOperation> startTreeNode, DirectoryInfo sourceStartDir)
        {
            // Files ==========================================================
            foreach(var sourceFile in sourceStartDir?.EnumerateFiles().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<FileInfo>())
            {
                var sourceFileTask = _worker.CreateWorkerTask(sourceFile);

                //rs: skip over any pages marked specifically as drafts
                if(sourceFileTask.TaskFlags.HasFlag(WorkerTaskFlags.IsDraft) && !_gaiusConfiguration.IsTestModeEnabled)
                    continue;

                var sourceFileOp = new FSOperation(sourceFileTask);
                startTreeNode.AddChild(sourceFileOp);
            }

            // Directories ====================================================
            foreach(var sourceDir in sourceStartDir?.EnumerateDirectories().OrderBy(directoryInfo => directoryInfo.Name) ?? Enumerable.Empty<DirectoryInfo>())
            {
                //rs: skip over the _drafts source folder if we're not in test mode
                if(sourceDir.FullName.Equals(_gaiusConfiguration.DraftsDirectoryFullPath) && !_gaiusConfiguration.IsTestModeEnabled)
                    continue;

                var sourceDirTask = _worker.CreateWorkerTask(sourceDir);
                var sourceDirOp = new FSOperation(sourceDirTask);
                var newOpTreeNode = startTreeNode.AddChild(sourceDirOp);
                AddSourceOperationsToTreeNode(newOpTreeNode, sourceDir);
            }
        }

        private void AddBaseNavDataToWorker(TreeNode<IOperation> sourceDirTreeNode, bool forSidebar)
        {
            //rs: get all worker tasks for all top level pages that should be included in the site navigation
            //1. Must have FrontMatter
            //2. Level == 0
            var topLevelBaseNavData = sourceDirTreeNode
                .Where(tn => !tn.Data.IsInvalid
                             && tn.Data.WorkerTask.GetHasOrder(forSidebar)
                             && tn.Data.WorkerTask.FrontMatter.GetLevel(forSidebar) == 0)
                .OrderBy(tn => tn.Data.WorkerTask.FrontMatter.GetOrder(forSidebar))
                .Select(tn => forSidebar
                              ? new SidebarData(tn.Data.WorkerTask) as BaseNavData
                              : new NavData(tn.Data.WorkerTask) as BaseNavData)
                .ToList();

            foreach(var baseNavData in topLevelBaseNavData)
            {
                AddChildBaseNavData(sourceDirTreeNode, baseNavData, baseNavData.Order, baseNavData.Level, forSidebar);
            }

            if(forSidebar)
                _worker.AddSidebarDataToWorker(topLevelBaseNavData);

            else
                _worker.AddNavDataToWorker(topLevelBaseNavData);
        }

        private void AddChildBaseNavData(TreeNode<IOperation> sourceDirTreeNode,
                                              BaseNavData parentBaseNavData, string parentOrder,
                                              int parentLevel, bool forSidebar)
        {
            var childBaseNavData = sourceDirTreeNode
                .Where(tn => !tn.Data.IsInvalid
                             && tn.Data.WorkerTask.GetHasOrder(forSidebar)
                             && tn.Data.WorkerTask.FrontMatter.GetOrder(forSidebar).StartsWith(parentOrder)
                             && tn.Data.WorkerTask.FrontMatter.GetLevel(forSidebar) == parentLevel + 1)
                .OrderBy(tn => tn.Data.WorkerTask.FrontMatter.GetOrder(forSidebar))
                .Select(tn => forSidebar 
                              ? new SidebarData(tn.Data.WorkerTask) as BaseNavData
                              : new NavData(tn.Data.WorkerTask) as BaseNavData)
                .ToList();

            if(childBaseNavData == null || childBaseNavData.Count == 0)
                return;

            parentBaseNavData.AddChildBaseNavData(childBaseNavData);

            foreach(var child in childBaseNavData)
            {
                AddChildBaseNavData(sourceDirTreeNode, child, child.Order, child.Level, forSidebar);
            }
        }

        private void AddAdditionalPostListingPaginatorOps(TreeNode<IOperation> sourceDirTreeNode)
        {
            //rs: get all post ops worker tasks
            var postWorkerTasks = sourceDirTreeNode
                .Where(tn => !tn.Data.IsInvalid
                             && tn.Data.WorkerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsPost))
                .Select(tn => tn.Data.WorkerTask).ToList();
            
            if(_gaiusConfiguration.IsTestModeEnabled)
            {
                var draftWorkerTasks = sourceDirTreeNode
                    .Where(tn => !tn.Data.IsInvalid
                                 && tn.Data.WorkerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsDraftPost))
                    .Select(tn => tn.Data.WorkerTask).ToList();

                postWorkerTasks.AddRange(draftWorkerTasks);
            }

            postWorkerTasks = postWorkerTasks.OrderByDescending(wt => wt.Date).ToList();
            
            var postListingOpNodes = sourceDirTreeNode
                .Where(tn => tn.Data.WorkerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsPostListing)
                        && !tn.Data.WorkerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsTagListing))
                .ToList();
            
            //rs: add additional paginator ops for any default post listing pages
            foreach(var postListingOpNode in postListingOpNodes)
            {
                AddAdditionalChildPaginatorOps(postListingOpNode, postWorkerTasks);
            }
        }

        private void AddTagDataToWorkerAndAdditionalTagListingOps(TreeNode<IOperation> sourceDirTreeNode)
        {
            var tags = sourceDirTreeNode.Where(tn => !tn.Data.IsInvalid && tn.Data.WorkerTask.HasFrontMatter)
                                    .SelectMany(tn => tn.Data.WorkerTask.FrontMatter.GetTags())
                                    .Distinct()
                                    .OrderBy(t => t)
                                    .ToList();

            var tagData = tags.Select(t => new TagData(t)).ToList();

            //rs: get first tag list op node
            var tagListOpTreeNode = sourceDirTreeNode.FirstOrDefault(tn => tn.Data.WorkerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsTagListing));

            _worker.AddTagDataToWorker(tagData, tagListOpTreeNode != null);

            if(tagListOpTreeNode == null)
                return;

            foreach(var tag in tags)
            {
                //rs: get all worker tasks associated with a given tag
                var tagWorkerTasks = sourceDirTreeNode
                    .Where(tn => !tn.Data.IsInvalid
                                 && tn.Data.WorkerTask.HasFrontMatter
                                 && tn.Data.WorkerTask.FrontMatter.GetTags().Contains(tag))
                    .OrderByDescending(tn => tn.Data.WorkerTask.Date)
                    .Select(tn => tn.Data.WorkerTask)
                    .ToList();

                AddAdditionalChildPaginatorOps(tagListOpTreeNode, tagWorkerTasks, tag);
            }
        }

        private void AddAdditionalChildPaginatorOps(TreeNode<IOperation> sourceOpTreeNode, List<WorkerTask> postWorkerTasks, string associatedTag = null)
        {
            if(postWorkerTasks.Count == 0)
                return;

            var itemsPerPage = _gaiusConfiguration.Pagination;
            var totalItems = postWorkerTasks.Count;
            var totalPages = totalItems / itemsPerPage;

            if(totalItems % itemsPerPage > 0)
                totalPages++;

            var firstPagePostWorkerTasks = postWorkerTasks.Take(itemsPerPage).ToList();
            var firstPagePaginatorData = new Paginator(itemsPerPage, 1, totalPages, totalItems, associatedTag);

            //rs: remove the originally created tree node because the operation was created with a paginator
            var originalFileSystemInfo = sourceOpTreeNode.Data.WorkerTask.FileSystemInfo;
            var parentOpTreeNode = sourceOpTreeNode.Parent;
            parentOpTreeNode.Children.Remove(sourceOpTreeNode);

            //rs: create a new worker task and FSOperation, this time with the paginator data
            var replacementWorkerTask = _worker.CreateWorkerTask(originalFileSystemInfo, firstPagePaginatorData, firstPagePostWorkerTasks);
            var replacementFSOperation = new FSOperation(replacementWorkerTask);
            sourceOpTreeNode = parentOpTreeNode.AddChild(replacementFSOperation);

            for(var pg = 2; pg <= totalPages; pg++)
            {
                var pgPostWorkerTasks = postWorkerTasks.Skip((pg - 1) * itemsPerPage).Take(itemsPerPage).ToList();
                var pgPaginatorData = new Paginator(itemsPerPage, pg, totalPages, totalItems, associatedTag);
                var additionalWorkerTask = _worker.CreateWorkerTask(sourceOpTreeNode.Data.WorkerTask.FileSystemInfo, pgPaginatorData, pgPostWorkerTasks);
                var additonalOp = new FSOperation(additionalWorkerTask);
                sourceOpTreeNode.AddChild(additonalOp);
            }
        }

        public void ProcessFSOperationTree(TreeNode<IOperation> rootTreeNode)
        {
            var siteDirFullPath = _gaiusConfiguration.SiteContainerFullPath;
            var genDirFullPath = _gaiusConfiguration.GenerationDirectoryFullPath;

            if(!Directory.Exists(genDirFullPath))
                Directory.CreateDirectory(genDirFullPath);

            var genDir = new DirectoryInfo(genDirFullPath);
            var genFileSystemInfos = genDir.EnumerateFileSystemInfos();

            //rs: delete *almost* all contained directories and files in the generation directory
            foreach(var genFileSystemInfo in genFileSystemInfos)
            {
                //rs: we have an explicit request to keep a specific file or directory
                if(_gaiusConfiguration.AlwaysKeep.Any(ak => ak.ToLowerInvariant().Equals(genFileSystemInfo.Name.ToLowerInvariant())))
                    continue;

                if(genFileSystemInfo.IsDirectory())
                    ((DirectoryInfo)genFileSystemInfo).Delete(true);

                else genFileSystemInfo.Delete();
            }
            
            rootTreeNode.Data.Status = OperationStatus.Complete;
            
            foreach(var opNode in rootTreeNode)
            {
                ProcessFSOpTreeNode(opNode);
            }
        }

        private void ProcessFSOpTreeNode(TreeNode<IOperation> opTreeNode)
        {
            //rs: nothing to do for this particular op
            if(opTreeNode.Data.NoActionRequired)
            {
                opTreeNode.Data.Status = OperationStatus.Complete;
                return;
            }

            else if(opTreeNode.Data.IsDirectoryOp)
            {
                var newDir = Directory.CreateDirectory(opTreeNode.Data.WorkerTask.TaskFullPath);
                opTreeNode.Data.Status = newDir != null ? OperationStatus.Complete : OperationStatus.Error;
                return;
            }

            //rs: this is a regular file op, process it
            ProcessFileFSOpTreeNode(opTreeNode);
        }

        private void ProcessFileFSOpTreeNode(TreeNode<IOperation> opTreeNode)
        {
            Directory.CreateDirectory(opTreeNode.Data.WorkerTask.TaskParentDirectory);

            if(opTreeNode.Data.WorkerTask.WorkType == WorkType.None)
            {
                opTreeNode.Data.WorkerTask.FileInfo.CopyTo(opTreeNode.Data.WorkerTask.TaskFullPath, true);
                opTreeNode.Data.Status = OperationStatus.Complete;
                return;
            }

            var fileContent = _worker.PerformWork(opTreeNode.Data.WorkerTask);

            using (var streamWriter = new StreamWriter(opTreeNode.Data.WorkerTask.TaskFullPath))
            {
                streamWriter.Write(fileContent);
            }
            
            opTreeNode.Data.Status = OperationStatus.Complete;
        }
    }
}