using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DataBlockConverter.Core.Dtos
{
    public class CustomDisplayDataType
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string? Icon { get; set; }

        [DataMember]
        public string? Name { get; set; }
    }
}
