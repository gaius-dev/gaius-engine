using System.Collections.Generic;
using Gaius.Utilities.DataStructures;

namespace Gaius.Core.Processing.FileSystem
{
    public interface IFSProcessor
    {
        TreeNode<FSOperation> CreateFSOperationTree();
        void ProcessFSOperationTree(TreeNode<FSOperation> opTree);
    }
}