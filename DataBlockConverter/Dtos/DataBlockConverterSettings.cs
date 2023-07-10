namespace DataBlockConverter.Core.Dtos
{
    public class DataBlockConverterSettings
    {
        public const string DataBlockConverter = "DataBlockConverter";
        public string NameFormatting { get; set; } = "[Block list] - {0}";
        public string AliasFormatting { get; set; } = "{0}BL";
    }
}