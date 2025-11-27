using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Notifications;

namespace AutoBlockList.Notifications
{
    public class ClearCacheHandler : INotificationHandler<ContentTypeChangedNotification>
    {
        private readonly IAppPolicyCache _runtimeCache;
        public ClearCacheHandler(AppCaches appCaches)
        {
            _runtimeCache = appCaches.RuntimeCache;
        }

        public void Handle(ContentTypeChangedNotification notification)
        {
            _runtimeCache.ClearByKey(AutoBlockListConstants.CacheKey);
        }
    }
}
