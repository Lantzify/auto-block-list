using AutoBlockList.Hubs;

namespace AutoBlockList.Services.interfaces
{
	public interface IAutoBlockListContext
	{
		IAutoBlockListHubClient? Client { get; }
		void SetClient(IAutoBlockListHubClient client);
		void ClearClient();
	}
}