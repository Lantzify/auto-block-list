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

		public void AddReport<TObject>(TObject item)
		{
			if (_hubContext == null || string.IsNullOrEmpty(_connectionId))
				return;

			var client = _hubContext.Clients.Client(_connectionId);
			if (client != null)
			{
				client.SendAsync("AddReport", item);
				return;
			}

			_hubContext.Clients.All.SendAsync("AddReport", item);
		}

		public void UpdateTask<TObject>(TObject item)
		{
			if (_hubContext == null || string.IsNullOrEmpty(_connectionId))
				return;

			var client = _hubContext.Clients.Client(_connectionId);
			if (client != null)
			{
				client.SendAsync("UpdateTask", item).Wait();
				return;
			}

			_hubContext.Clients.All.SendAsync("UpdateTask", item).Wait();
		}


		public void UpdateItem<TObject>(TObject item)
		{
			if (_hubContext == null || string.IsNullOrEmpty(_connectionId))
				return;

			var client = _hubContext.Clients.Client(_connectionId);
			if (client != null)
			{
				client.SendAsync("UpdateItem", item).Wait();
				return;
			}

			_hubContext.Clients.All.SendAsync("UpdateItem", item).Wait();
		}

		public void Done()
		{
			if (_hubContext == null || string.IsNullOrEmpty(_connectionId))
				return;

			var client = _hubContext.Clients.Client(_connectionId);
			if (client != null)
			{
				client.SendAsync("Done").Wait();
				return;
			}

			_hubContext.Clients.All.SendAsync("Done").Wait();
		}
	}
}
