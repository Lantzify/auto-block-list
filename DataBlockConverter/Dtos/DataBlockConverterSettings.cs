using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DataBlockConverter.Core.Dtos
{
    public class DataBlockConverterSettings
    {
        public const string DataBlockConverter = "DataBlockConverter";
        public string NameFormatting { get; set; } = "[Block list] - {0}";
    }
}
