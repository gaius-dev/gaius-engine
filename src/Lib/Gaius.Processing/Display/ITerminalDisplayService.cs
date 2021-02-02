
using System.Collections.Generic;
using Gaius.Core.DataStructures;

namespace Gaius.Processing.Display
{
    public interface ITerminalDisplayService
    {
        void PrintOperationTree(TreeNode<IOperation> genOpTree);
    }
}