using AutoBlockList.Hubs;
using AutoBlockList.Services.interfaces;

namespace AutoBlockList.Services
{
    public class AutoBlockListContext : IAutoBlockListContext
    {
        private IAutoBlockListHubClient? client;

        public IAutoBlockListHubClient? Client => client;

        public void SetClient(IAutoBlockListHubClient client)
        {
            client = client;
        }

        public void ClearClient()
        {
            client = null;
        }
    }
}