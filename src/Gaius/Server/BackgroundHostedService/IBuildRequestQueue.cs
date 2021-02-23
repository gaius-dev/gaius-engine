using System.Threading;
using System.Threading.Tasks;

namespace Gaius.Server.BackgroundHostedService
{
    public interface IBuildRequestQueue
    {
        void QueueBuildRequest(BuildRequest buildRequest);

        Task<BuildRequest> DequeueBuildRequestAsync(CancellationToken cancellationToken);
    }
}
