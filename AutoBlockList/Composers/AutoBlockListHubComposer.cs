using System;
using System.Linq;
using System.Text;
using Umbraco.Extensions;
using AutoBlockList.Hubs;
using System.Threading.Tasks;
using System.Collections.Generic;
using Umbraco.Cms.Core.Composing;
using Microsoft.AspNetCore.Builder;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace AutoBlockList.Composers
{
	public class AutoBlockListHubComposer : IComposer
	{
		public void Compose(IUmbracoBuilder builder)
		{
			builder.Services.AddSingleton<AutoBlockListHubRoutes>();
			builder.Services.AddSignalR();
			builder.Services.Configure<UmbracoPipelineOptions>(options =>
			{
				options.AddFilter(new UmbracoPipelineFilter(
					"AutoBlockList",
					applicationBuilder => { },
					applicationBuilder => { },
					applicationBuilder =>
					{
						applicationBuilder.UseEndpoints(e =>
						{
							var hubRoutes = applicationBuilder.ApplicationServices.GetRequiredService<AutoBlockListHubRoutes>();
							hubRoutes.CreateRoutes(e);
						});
					}
				));
			});
		}
	}
}
