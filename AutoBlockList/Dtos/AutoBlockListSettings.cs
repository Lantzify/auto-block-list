namespace AutoBlockList.Dtos
{
    public class AutoBlockListSettings
    {
        public const string AutoBlockList = "AutoBlockList";
        public string BlockListEditorSize { get; set; } = "medium";
        public bool SaveAndPublish { get; set; } = true;
        public string NameFormatting { get; set; } = "[Block list] - {0}";
        public string AliasFormatting { get; set; } = "{0}BL";
        public string FolderNameForContentTypes { get; set; } = "[Rich text editor] - Components";
    }
}