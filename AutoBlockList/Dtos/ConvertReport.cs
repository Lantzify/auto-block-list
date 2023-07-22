using System.Runtime.Serialization;
using static AutoBlockList.Constants.AutoBlockListConstants;

namespace AutoBlockList.Dtos
{
    public class ConvertReport
    {
        [DataMember]
        public string? Task { get; set; }

        [DataMember]
        public Status Status { get; set; }

        [DataMember]
        public string? ErrorMessage { get; set; }
    }
}
