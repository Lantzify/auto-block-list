using Umbraco.Extensions;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Cms.Core.Configuration.Models;

namespace AutoBlockList.Hubs
{
	public class AutoBlockListHubRoutes : IAreaRoutes
	{
		private readonly IRuntimeState _runtimeState;
		private readonly string _umbracoPathSegment;
		public AutoBlockListHubRoutes(IOptions<GlobalSettings> globalSettings,
				IHostingEnvironment hostingEnvironment,
				IRuntimeState runtimeState)
		{
			_runtimeState = runtimeState;
			_umbracoPathSegment = globalSettings.Value.GetUmbracoMvcArea(hostingEnvironment);
		}

		public void CreateRoutes(IEndpointRouteBuilder endpoints)
		{
			switch (_runtimeState.Level)
			{
				case Umbraco.Cms.Core.RuntimeLevel.Run:
					endpoints.MapHub<AutoBlockListHub>(GetAutoBlockListHubRoute());
					break;
			}
		}

		public string GetAutoBlockListHubRoute() => $"/{_umbracoPathSegment}/AutoBlockList/SyncHub";
	}
}
