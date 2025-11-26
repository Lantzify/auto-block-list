using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using AutoBlockList.Constants;
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
			if (notification.SavedEntities.Any(x => x.Properties.Any(p => p.PropertyType.PropertyEditorAlias == PropertyEditors.Aliases.TinyMce)))
				_runtimeCache.ClearByKey("AutoBlockListContentTypesTinyMCE_Page_");
		}
	}
}
