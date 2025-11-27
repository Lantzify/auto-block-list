using AutoBlockList.Dtos;
using AutoBlockList.Services;
using AutoBlockList.Backoffice;
using Umbraco.Cms.Core.Composing;
using AutoBlockList.Notifications;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AutoBlockList.Composers
{
    public class AutoBlockListComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.ManifestFilters().Append<AutoBlockListManifestFilter>();

            builder.AddNotificationHandler<ContentTypeChangedNotification, ClearCacheHandler>();

            builder.Services.AddSingleton<IAutoBlockListService, AutoBlockListService>();

            builder.Services.AddOptions<AutoBlockListSettings>()
                .Bind(builder.Config.GetSection(AutoBlockListSettings.AutoBlockList));
        }
    }
}
