using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
using System.Collections.Generic;

namespace AutoBlockList.Dtos
{
    public class AddDataTypeToContentTypeDto
    {
        public int OldDataTypeId { get; set; }
        public int NewDataTypeId { get; set; }
        public int ContentTypeId { get; set; }
    }
}
