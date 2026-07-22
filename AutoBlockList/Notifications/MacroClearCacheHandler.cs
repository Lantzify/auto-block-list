using Umbraco.Cms.Core.Cache;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace AutoBlockList.Notifications
{
    public class MacroClearCacheHandler : INotificationHandler<MacroDeletedNotification>
    {
        private readonly IAppPolicyCache _runtimeCache;
        public MacroClearCacheHandler(AppCaches appCaches)
        {
            _runtimeCache = appCaches.RuntimeCache;
        }

		public void Handle(MacroDeletedNotification notification)
		{
			_runtimeCache.Clear(AutoBlockListConstants.TinyMCECacheKey);
			_runtimeCache.ClearByKey("AutoBlockListContentTypesTinyMCE_Page_");
		}
	}
}