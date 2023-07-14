using System.Runtime.Serialization;

namespace AutoBlockList.Dtos
{
    public class CustomDisplayDataType
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string? Icon { get; set; }

        [DataMember]
        public string? Name { get; set; }

        [DataMember]
        public int? MatchingBLId { get; set; }
    }
}
