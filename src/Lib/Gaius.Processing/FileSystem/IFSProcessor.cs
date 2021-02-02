using Gaius.Core.DataStructures;

namespace Gaius.Processing.FileSystem
{
    public interface IFSProcessor
    {
        TreeNode<IOperation> CreateFSOperationTree();
        void ProcessFSOperationTree(TreeNode<IOperation> opTree);
    }
}