using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Trees;
using Umbraco.Cms.Core.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Trees;
using Umbraco.Cms.Web.Common.Attributes;
using static Umbraco.Cms.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Web.Common.ModelBinders;
using Umbraco.Cms.Web.Common.Authorization;

namespace DataBlockConverter.Core.Backoffice
{
    [PluginController("DataBlockConverter")]
    [Authorize(Policy = AuthorizationPolicies.TreeAccessDocumentTypes)]
    [Tree("settings", "dataBlockConverter", SortOrder = 12, TreeTitle = "Data block converter", TreeGroup = "dataBlockConverterGroup")]
    public class DataBlockConverterTreeController : TreeController
    {
        private readonly IMenuItemCollectionFactory _menuItemCollectionFactory;
        public DataBlockConverterTreeController(IMenuItemCollectionFactory menuItemCollectionFactory, ILocalizedTextService localizedTextService, UmbracoApiControllerTypeCollection umbracoApiControllerTypeCollection, IEventAggregator eventAggregator) : base(localizedTextService, umbracoApiControllerTypeCollection, eventAggregator)
        {
            _menuItemCollectionFactory = menuItemCollectionFactory;
        }


        protected override ActionResult<TreeNode> CreateRootNode(FormCollection queryStrings)
        {
            var rootResult = base.CreateRootNode(queryStrings);
            if (!(rootResult.Result is null))
                return rootResult;

            var root = rootResult.Value;

            root.RoutePath = string.Format("{0}/{1}/{2}", Applications.Settings, "dataBlockConverter", "overview");
            root.Icon = "icon-sience";
            root.HasChildren = false;
            root.MenuUrl = null;

            return root;
        }


        protected override ActionResult<MenuItemCollection> GetMenuForNode(string id, [ModelBinder(typeof(HttpQueryStringModelBinder))] FormCollection queryStrings)
        {
            var menu = _menuItemCollectionFactory.Create();
            return menu;
        }

        protected override ActionResult<TreeNodeCollection> GetTreeNodes(string id, [ModelBinder(typeof(HttpQueryStringModelBinder))] FormCollection queryStrings)
        {
            var nodes = new TreeNodeCollection();
            return nodes;
        }
    }
}
