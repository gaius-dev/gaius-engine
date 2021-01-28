using System.Collections.Generic;
using Gaius.Core.Worker;

namespace Gaius.Core.Models
{
    public class ViewModelData : BaseViewModelData
    {
        public ViewModelData(WorkerTask workerTask, string content, PaginatorData paginatorData = null, List<BaseViewModelData> paginatorViewModels = null) : base(workerTask, content)
        {
            PaginatorData = paginatorData;
            PaginatorViewModels = paginatorViewModels;
        }

        public PaginatorData PaginatorData { get; private set; }
        public List<BaseViewModelData> PaginatorViewModels { get; private set;}
    }
}