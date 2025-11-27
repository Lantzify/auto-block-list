using Umbraco.Cms.Core.Manifest;

namespace AutoBlockList.Backoffice
{
    public class AutoBlockListManifestFilter : IManifestFilter
    {
        public void Filter(List<PackageManifest> manifests)
        {
            manifests.Add(new PackageManifest
            {
                PackageName = "AutoBlockList",
                Scripts = new[]
                {
                    "/App_Plugins/AutoBlockList/backoffice/autoBlockList/overview.controller.js",
                    "/App_Plugins/AutoBlockList/components/overlays/converting.controller.js"
                },
                Stylesheets = new[]
                {
                    "/App_Plugins/AutoBlockList/autoBlockList.css",
                }
            });
        }
    }
}
