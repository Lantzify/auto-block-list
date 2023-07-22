using System.Runtime.Serialization;

namespace AutoBlockList.Dtos
{
	public class ConvertDto
	{
		[DataMember]
		public IEnumerable<AutoBlockListContent>? Contents { get; set; }
        
		[DataMember]
		public string? ConnectionId { get; set; }
    }
}