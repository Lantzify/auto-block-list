namespace AutoBlockList.Constants
{
    public static class AutoBlockListConstants
    {
        public const string CacheKey = "AutoBlockListContentTypes";
        public static readonly string[] DefaultNC = {
            "name",
            "ncContentTypeAlias",
            "PropType",
            "key"
        };

        public enum Status
        {
            Success,
            Skipped,
            Failed
        };

    }
}