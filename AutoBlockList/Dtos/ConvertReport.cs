using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
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

        [DataMember]
        public object? Item { get; set; }
    }
}
