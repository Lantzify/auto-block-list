using Umbraco.Cms.Core.Cache;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace AutoBlockList.Notifications
{
    public class ContentClearCacheHandler : INotificationHandler<ContentSavedNotification>,
											INotificationHandler<ContentDeletedNotification>
    {
        private readonly IAppPolicyCache _runtimeCache;
        public ContentClearCacheHandler(AppCaches appCaches)
        {
            _runtimeCache = appCaches.RuntimeCache;
        }

		public void Handle(ContentSavedNotification notification)
		{
			if (notification.SavedEntities.Any(x => x.Properties.Any(p => AutoBlockListConstants.RichTextEditor_And_BlockListAlias.Contains(p.PropertyType.PropertyEditorAlias))))
				_runtimeCache.ClearByKey("AutoBlockListContentTypesTinyMCE_Page_");
		}

		public void Handle(ContentDeletedNotification notification)
		{
			if (notification.DeletedEntities.Any(x => x.Properties.Any(p => AutoBlockListConstants.RichTextEditor_And_BlockListAlias.Contains(p.PropertyType.PropertyEditorAlias))))
				_runtimeCache.ClearByKey("AutoBlockListContentTypesTinyMCE_Page_");
		}
	}
}
