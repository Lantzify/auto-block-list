namespace AutoBlockList.Dtos
{
    public class AutoBlockListSettings
    {
        public const string AutoBlockList = "AutoBlockList";
        public string NameFormatting { get; set; } = "[Block list] - {0}";
        public string AliasFormatting { get; set; } = "{0}BL";
    }
}