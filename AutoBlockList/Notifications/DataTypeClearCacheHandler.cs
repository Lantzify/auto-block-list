using Umbraco.Cms.Core.Cache;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using static Umbraco.Cms.Core.Constants;

namespace AutoBlockList.Notifications
{
    public class DataTypeClearCacheHandler : INotificationHandler<DataTypeDeletedNotification>
    {
        private readonly IAppPolicyCache _runtimeCache;
        public DataTypeClearCacheHandler(AppCaches appCaches)
        {
            _runtimeCache = appCaches.RuntimeCache;
        }

		public void Handle(DataTypeDeletedNotification notification)
		{
			if (notification.DeletedEntities.Any(x => x.EditorAlias == PropertyEditors.Aliases.NestedContent))
				_runtimeCache.Clear(AutoBlockListConstants.CacheKey);
			
		}
	}
}