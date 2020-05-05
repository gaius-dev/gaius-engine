using System.Collections.Generic;
using Strube.Utilities.DataStructures;

namespace Gaius.Core.Processing.FileSystem
{
    public interface IFSProcessor
    {
        (bool, List<string>) ValidateSiteContainerDir();
        TreeNode<FSOperation> CreateFSOperationTree();
        void ProcessFSOperationTree(TreeNode<FSOperation> opTree);
    }
}