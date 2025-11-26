using Microsoft.AspNetCore.SignalR;

namespace AutoBlockList.Hubs
{
	public class AutoBlockListHubClient : IAutoBlockListHubClient
	{
		private readonly IHubContext<AutoBlockListHub> _hubContext;
		private readonly string _connectionId;


		public AutoBlockListHubClient(IHubContext<AutoBlockListHub> hubContext,
			string connectionId)
		{
			_hubContext = hubContext;
			_connectionId = connectionId;
		}

        private void Send<TObject>(string method, TObject item)
        {
            if (_hubContext == null || string.IsNullOrEmpty(_connectionId))
                return;

            var client = _hubContext.Clients.Client(_connectionId);
            if (client != null)
            {
                client.SendAsync(method, item).Wait();
                return;
            }

            _hubContext.Clients.All.SendAsync(method, item).Wait();
        }

        public void AddReport<TObject>(TObject item)
		{
			Send("AddReport", item);
		}

        public void CurrentTask<TObject>(TObject item)
        {
            Send("CurrentTask", item);
        }

        public void UpdateStep<TObject>(TObject item)
		{
            Send("UpdateStep", item);
		}


		public void UpdateItem<TObject>(TObject item)
		{
            Send("UpdateItem", item);
		}

        public void SetTitle<TObject>(TObject item)
        {
            Send("SetTitle", item);
        }

        public void SetSubTitle<TObject>(TObject item)
        {
            Send("SetSubTitle", item);
        }


        public void Done<TObject>(TObject item)
		{
            Send("Done", item);
		}
	}
}
