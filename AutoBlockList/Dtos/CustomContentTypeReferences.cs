namespace AutoBlockList.Dtos
{
    public class CustomContentTypeReferences
    {
        public int Id { get; set; }
        public Guid Key { get; set; }
        public string? Alias { get; set; }
        public string? Icon { get; set; }
        public string? Name { get; set; }
        public bool IsElement { get; set; }

    }
}