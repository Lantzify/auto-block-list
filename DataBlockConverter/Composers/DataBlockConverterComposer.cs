using Umbraco.Cms.Core.Composing;
using DataBlockConverter.Core.Dtos;
using DataBlockConverter.Core.Services;
using DataBlockConverter.Core.Backoffice;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DataBlockConverter.Core.Composers
{
    public class DataBlockConverterComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.ManifestFilters().Append<DataBlockConverterManifestFilter>();

            builder.Services.AddSingleton<IDataBlockConverterService, DataBlockConverterService>();

            builder.Services.AddOptions<DataBlockConverterSettings>()
                .Bind(builder.Config.GetSection(DataBlockConverterSettings.DataBlockConverter));
        }
    }
}
