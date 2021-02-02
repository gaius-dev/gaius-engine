using System.Collections.Generic;

namespace Gaius.Worker.Models
{
    public class ViewModel : BaseViewModel
    {
        public ViewModel(WorkerTask workerTask, string content, Paginator paginatorData = null, List<BaseViewModel> paginatorViewModels = null) : base(workerTask, content)
        {
            Paginator = paginatorData;
            PaginatorViewModels = paginatorViewModels;
        }

        public Paginator Paginator { get; private set; }
        public List<BaseViewModel> PaginatorViewModels { get; private set; }
    }
}