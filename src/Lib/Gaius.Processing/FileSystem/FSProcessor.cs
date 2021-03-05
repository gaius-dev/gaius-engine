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
            AddNavDataToWorker(sourceDirTreeNode);
            AddAdditionalPostListingPaginatorOps(sourceDirTreeNode);
            AddTagDataToWorkerAndAdditionalTagListingOps(sourceDirTreeNode);
            
            var namedThemeDirTreeNode = rootTreeNode.AddChild(namedThemeDirOp);
            AddSourceOperationsToTreeNode(namedThemeDirTreeNode, namedThemeDirTask.DirectoryInfo);

            var genDirectoryInfo = new DirectoryInfo(_gaiusConfiguration.GenerationDirectoryFullPath);
            AddGenerationDirOperationsToTreeNode(rootTreeNode, rootTreeNode, genDirectoryInfo);

            PruneNullGenDirOpNodes(rootTreeNode);

            return rootTreeNode;            
        }

        private void AddSourceOperationsToTreeNode(TreeNode<IOperation> startTreeNode, DirectoryInfo sourceStartDir)
        {
            // Files ==========================================================
            foreach(var sourceFile in sourceStartDir?.EnumerateFiles().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<FileInfo>())
            {
                var sourceFileTask = _worker.CreateWorkerTask(sourceFile);
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

        private void AddNavDataToWorker(TreeNode<IOperation> sourceDirTreeNode)
        {
            //rs: get all worker tasks for all top level pages that should be included in the site navigation
            //1. Must have FrontMatter
            //2. Level == 0
            var topLevelNavData = sourceDirTreeNode
                .Where(tn => !tn.Data.IsInvalid
                             && tn.Data.WorkerTask.HasNavOrder
                             && tn.Data.WorkerTask.FrontMatter.NavLevel == 0)
                .OrderBy(tn => tn.Data.WorkerTask.FrontMatter.NavOrder)
                .Select(tn => new NavData(tn.Data.WorkerTask))
                .ToList();

            foreach(var navData in topLevelNavData)
            {
                AddChildNavDataToNavData(sourceDirTreeNode, navData, navData.Order, navData.Level);
            }

            _worker.AddNavDataToWorker(topLevelNavData);
        }

        private void AddChildNavDataToNavData(TreeNode<IOperation> sourceDirTreeNode, NavData parentNavData, string parentNavOrder, int parentLevel)
        {
            var childNavData = sourceDirTreeNode
                .Where(tn => !tn.Data.IsInvalid
                             && tn.Data.WorkerTask.HasNavOrder
                             && tn.Data.WorkerTask.FrontMatter.NavOrder.Contains(parentNavOrder)
                             && tn.Data.WorkerTask.FrontMatter.NavLevel == parentLevel + 1)
                .OrderBy(tn => tn.Data.WorkerTask.FrontMatter.NavOrder)
                .Select(tn => new NavData(tn.Data.WorkerTask))
                .ToList();

            if(childNavData == null || childNavData.Count == 0)
                return;

            parentNavData.AddChildNavData(childNavData);

            foreach(var child in childNavData)
            {
                AddChildNavDataToNavData(sourceDirTreeNode, child, child.Order, child.Level);
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
                                 && tn.Data.WorkerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsDraft))
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

        private void AddGenerationDirOperationsToTreeNode(TreeNode<IOperation> rootTreeNode, TreeNode<IOperation> startTreeNode, DirectoryInfo generationStartDir)
        {
            if(!generationStartDir.Exists)
                return;
                
            // Files ==========================================================
            foreach(var genFile in generationStartDir?.EnumerateFiles().OrderBy(fileInfo => fileInfo.Name) ?? Enumerable.Empty<FileInfo>())
            {
                if(!rootTreeNode.Any(node => !node.Data.IsNullOp && node.Data.WorkerTask.TaskFullPath == genFile.FullName))
                {
                    var genFileTask = _worker.CreateWorkerTask(genFile);
                    var genFileOp = new FSOperation(genFileTask);
                    startTreeNode.AddChild(genFileOp);
                }
            }

            // Directories ====================================================
            foreach(var genDir in generationStartDir?.EnumerateDirectories().OrderBy(directoryInfo => directoryInfo.Name) ?? Enumerable.Empty<DirectoryInfo>())
            {
                //rs: does the gen dir op have a matching source dir op?
                var hasMatchingSourceDirOp = rootTreeNode.Any(node => !node.Data.IsNullOp && node.Data.WorkerTask.TaskFullPath == genDir.FullName);

                //rs: check a special condition first
                // 1. We don't have an explicit matching source dir op
                // 2. AND we don't have a source file op which contains this gen directory (e.g. post and draft file ops)
                // This means we'll need to either delet or keep this generation directory
                if(!hasMatchingSourceDirOp && !rootTreeNode.Any(node => !node.Data.IsNullOp && node.Data.WorkerTask.TaskParentDirectory.Contains(genDir.FullName)))
                {
                    var genDirTask = _worker.CreateWorkerTask(genDir);
                    var genDirOp = new FSOperation(genDirTask);
                    var newGenDirOpNode = startTreeNode.AddChild(genDirOp);
                    AddGenerationDirOperationsToTreeNode(rootTreeNode, newGenDirOpNode, genDir);
                }

                //rs: we do have a matching dir op, just skip this gen dir and add nothing to the op tree
                else if(hasMatchingSourceDirOp)
                    continue;

                //rs: we don't really know if we'll need this gen dir op or not, add it as a null op
                //NOTE: this could be pruned later if it doesn't contain any child op nodes that are deletes or keeps
                else
                {
                    var nullGenDirOp = new FSOperation(genDir.Name, genDir.Name);
                    var newNullGenDirOpNode = startTreeNode.AddChild(nullGenDirOp);
                    AddGenerationDirOperationsToTreeNode(rootTreeNode, newNullGenDirOpNode, genDir);
                }
            }
        }

        private void PruneNullGenDirOpNodes(TreeNode<IOperation> rootTreeNode)
        {
            var level1NullGenDirOps = rootTreeNode.Where(node => node.Data.IsNullOp).ToList();

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

        public void ProcessFSOperationTree(TreeNode<IOperation> rootTreeNode)
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
                if(rootTreeNode.Any(node => !node.Data.IsNullOp && node.Data.WorkerTask.TaskFullPath == containedFsInfo.FullName
                    && node.Data.OperationType == OperationType.Keep))
                    continue;

                if(containedFsInfo.IsDirectory())
                    ((DirectoryInfo)containedFsInfo).Delete(true);

                else containedFsInfo.Delete();
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