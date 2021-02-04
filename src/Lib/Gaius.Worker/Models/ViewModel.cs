using System.Collections.Generic;

namespace Gaius.Worker.Models
{
    internal class ViewModel : BaseViewModel
    {
        internal ViewModel(WorkerTask workerTask, string content, Paginator paginatorData = null, List<BaseViewModel> paginatorViewModels = null) : base(workerTask, content)
        {
            Paginator = paginatorData;
            PaginatorViewModels = paginatorViewModels;
        }

        internal Paginator Paginator { get; private set; }
        internal List<BaseViewModel> PaginatorViewModels { get; private set; }
    }
}