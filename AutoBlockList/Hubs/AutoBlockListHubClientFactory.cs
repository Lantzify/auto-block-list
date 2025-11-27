using Microsoft.AspNetCore.SignalR;

namespace AutoBlockList.Hubs
{
    public interface IAutoBlockListHubClientFactory
    {
        IAutoBlockListHubClient CreateClient(string connectionId);
    }

    public class AutoBlockListHubClientFactory : IAutoBlockListHubClientFactory
    {
        private readonly IHubContext<AutoBlockListHub> _hubContext;

        public AutoBlockListHubClientFactory(IHubContext<AutoBlockListHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public IAutoBlockListHubClient CreateClient(string connectionId)
        {
            return new AutoBlockListHubClient(_hubContext, connectionId);
        }
    }
}