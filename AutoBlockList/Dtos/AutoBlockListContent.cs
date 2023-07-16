using Umbraco.Cms.Core.Models;
using System.Runtime.Serialization;

namespace AutoBlockList.Dtos
{
    public class AutoBlockListContent
    {
        [DataMember]
        public string? Name { get; set; }

        [DataMember]
        public ISimpleContentType? ContentType { get; set; }
    }
}
