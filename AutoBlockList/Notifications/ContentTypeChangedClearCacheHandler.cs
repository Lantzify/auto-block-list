using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Notifications;
using static Umbraco.Cms.Core.Constants;

namespace AutoBlockList.Notifications
{
    public class ContentTypeChangedClearCacheHandler : INotificationHandler<ContentTypeChangedNotification>
    {
        private readonly IAppPolicyCache _runtimeCache;
        public ContentTypeChangedClearCacheHandler(AppCaches appCaches)
        {
            _runtimeCache = appCaches.RuntimeCache;
        }

        public void Handle(ContentTypeChangedNotification notification)
        {
			if (notification.Changes.Any(x => x.Item.PropertyTypes.Any(p => p.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent || p.PropertyEditorAlias == PropertyEditors.Aliases.BlockList)))
				_runtimeCache.Clear(AutoBlockListConstants.CacheKey);

            if(notification.Changes.Any(x => x.Item.PropertyTypes.Any(p => AutoBlockListConstants.RichTextEditor_And_BlockListAlias.Contains(p.PropertyEditorAlias))))
                _runtimeCache.Clear(AutoBlockListConstants.TinyMCECacheKey);            
        }
    }
}
