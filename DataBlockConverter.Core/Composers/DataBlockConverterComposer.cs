using System;
using System.Linq;
using System.Text;
using Umbraco.Extensions;
using System.Threading.Tasks;
using System.Collections.Generic;
using Umbraco.Cms.Core.Composing;
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
        }
    }
}
