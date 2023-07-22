using Umbraco.Cms.Core.Models;
using System.Runtime.Serialization;

namespace AutoBlockList.Dtos
{
    public class AutoBlockListContent
    {
		[DataMember]
		public int Id { get; set; }

		[DataMember]
        public string? Name { get; set; }

		[DataMember]
		public bool HasBLAssociated { get; set; }

		[DataMember]
		public Guid ContentTypeKey { get; set; }
    }
}
