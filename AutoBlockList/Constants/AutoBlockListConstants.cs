namespace AutoBlockList.Constants
{
    public static class AutoBlockListConstants
    {
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