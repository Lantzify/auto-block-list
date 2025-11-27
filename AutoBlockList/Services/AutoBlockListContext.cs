using AutoBlockList.Hubs;
using AutoBlockList.Services.interfaces;

namespace AutoBlockList.Services
{
    public class AutoBlockListContext : IAutoBlockListContext
    {
        private IAutoBlockListHubClient? _client;

        public IAutoBlockListHubClient? Client => _client;

        public void SetClient(IAutoBlockListHubClient client)
        {
            _client = client;
        }

        public void ClearClient()
        {
            _client = null;
        }
    }
}