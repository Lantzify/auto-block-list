using AutoBlockList.Dtos;
using AutoBlockList.Services;
using AutoBlockList.Backoffice;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AutoBlockList.Composers
{
    public class AutoBlockListComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.ManifestFilters().Append<AutoBlockListManifestFilter>();

            builder.Services.AddSingleton<IAutoBlockListService, AutoBlockListService>();

            builder.Services.AddOptions<AutoBlockListSettings>()
                .Bind(builder.Config.GetSection(AutoBlockListSettings.DataBlockConverter));
        }
    }
}
