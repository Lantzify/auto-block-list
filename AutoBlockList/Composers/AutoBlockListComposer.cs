using AutoBlockList.Dtos;
using AutoBlockList.Hubs;
using AutoBlockList.Services;
using AutoBlockList.Backoffice;
using Umbraco.Cms.Core.Composing;
using AutoBlockList.Notifications;
using Umbraco.Cms.Core.Notifications;
using AutoBlockList.Services.interfaces;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AutoBlockList.Composers
{
    public class AutoBlockListComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.ManifestFilters().Append<AutoBlockListManifestFilter>();

            builder.AddNotificationHandler<ContentTypeChangedNotification, ContentTypeChangedClearCacheHandler>()
                    .AddNotificationHandler<ContentSavedNotification, ContentSavedClearCacheHandler>();

			builder.Services.AddSingleton<IAutoBlockListHubClientFactory, AutoBlockListHubClientFactory>()
							.AddScoped<IAutoBlockListContext, AutoBlockListContext>();

			builder.Services.AddScoped<IAutoBlockListService, AutoBlockListService>()
                            .AddScoped<IAutoBlockListMacroService, AutoBlockListMacroService>();

            builder.Services.AddOptions<AutoBlockListSettings>()
                .Bind(builder.Config.GetSection(AutoBlockListSettings.AutoBlockList));
        }
    }
}
