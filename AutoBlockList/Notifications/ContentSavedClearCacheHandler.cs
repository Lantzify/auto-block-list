using Umbraco.Cms.Core.Cache;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using static Umbraco.Cms.Core.Constants;

namespace AutoBlockList.Notifications
{
    public class ContentSavedClearCacheHandler : INotificationHandler<ContentSavedNotification>
    {
        private readonly IAppPolicyCache _runtimeCache;
        public ContentSavedClearCacheHandler(AppCaches appCaches)
        {
            _runtimeCache = appCaches.RuntimeCache;
        }

		public void Handle(ContentSavedNotification notification)
		{
			if (notification.SavedEntities.Any(x => x.Properties.Any(p => AutoBlockListConstants.RichTextEditor_And_BlockListAlias.Contains(p.PropertyType.PropertyEditorAlias))))
				_runtimeCache.ClearByKey("AutoBlockListContentTypesTinyMCE_Page_");
		}
	}
}
