using Gaius.Core.DataStructures;

namespace Gaius.Processing.FileSystem
{
    public interface IFSProcessor
    {
        TreeNode<FSOperation> CreateFSOperationTree();
        void ProcessFSOperationTree(TreeNode<FSOperation> opTree);
    }
}