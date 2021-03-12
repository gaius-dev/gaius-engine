using System.Collections.Generic;

namespace Gaius.Worker.Models
{
    public abstract class BaseNavData
    {
        public string Id { get; protected set; }
        public string Title { get; protected set; }
        public string Url { get; protected set; }
        public string Order { get; protected set; }
        public int Level { get; protected set; }
        public List<BaseNavData> Children { get; protected set; }

        public void AddChildBaseNavData(List<BaseNavData> childBaseNavData)
        {
            Children = childBaseNavData;
        }
    }
}