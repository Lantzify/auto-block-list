using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using static DataBlockConverter.Constants.DataBlockConverterConstants;

namespace DataBlockConverter.Dtos
{
	public class ConvertReport
	{
        public string? Task { get; set; }
        public Status Status { get; set; }
        public string? ErrorMessage { get; set; }
        public object Item { get; set; }
    }
}
