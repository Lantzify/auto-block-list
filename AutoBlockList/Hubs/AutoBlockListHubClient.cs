using Microsoft.AspNetCore.SignalR;

namespace AutoBlockList.Hubs
{
	public class AutoBlockListHubClient
	{
		private readonly IHubContext<AutoBlockListHub> _hubContext;
		private readonly string _connectionId;


		public AutoBlockListHubClient(IHubContext<AutoBlockListHub> hubContext,
			string connectionId)
		{
			_hubContext = hubContext;
			_connectionId = connectionId;
		}

        private async Task SendAsync<TObject>(string method, TObject item)
        {
            if (_hubContext == null || string.IsNullOrEmpty(_connectionId))
                return;

            var client = _hubContext.Clients.Client(_connectionId);
            if (client != null)
            {
                await client.SendAsync(method, item);
                return;
            }

            await _hubContext.Clients.All.SendAsync(method, item);
        }

        public async Task AddReport<TObject>(TObject item)
		{
			await SendAsync("AddReport", item);
		}

        public async Task CurrentTask<TObject>(TObject item)
        {
			await SendAsync("CurrentTask", item);
        }

        public async Task UpdateStep<TObject>(TObject item)
		{
			await SendAsync("UpdateStep", item);
		}


		public async Task UpdateItem<TObject>(TObject item)
		{
			await SendAsync("UpdateItem", item);
		}

        public async Task SetTitle<TObject>(TObject item)
        {
			await SendAsync("SetTitle", item);
        }

        public async Task SetSubTitle<TObject>(TObject item)
        {
			await SendAsync("SetSubTitle", item);
        }


        public async Task Done<TObject>(TObject item)
		{
			await SendAsync("Done", item);
		}
	}
}
