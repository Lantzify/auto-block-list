using Umbraco.Cms.Core.Manifest;

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
                    "/App_Plugins/DataBlockConverter/backoffice/dataBlockConverter/overview.controller.js",
					"/App_Plugins/DataBlockConverter/components/overlays/converting.controller.js"
				},
                Stylesheets = new[]
                {
					"/App_Plugins/DataBlockConverter/dataBlockConverter.css",
				}
			});
        }
    }
}
