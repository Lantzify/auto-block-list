using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Manifest;
using System.Collections.Generic;

namespace DataBlockConverter.Core.Backoffice
{
    public class DataBlockConverterManifestFilter : IManifestFilter
    {
        public void Filter(List<PackageManifest> manifests)
        {
            manifests.Add(new PackageManifest
            {
                PackageName = "DataBlockConverter",
                Scripts = new[]
                {
                    "/App_Plugins/DataBlockConverter/backoffice/dataBlockConverter/overview.controller.js"
                }
            });
        }
    }
}
