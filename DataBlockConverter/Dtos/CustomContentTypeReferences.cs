using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DataBlockConverter.Core.Dtos
{
    public class CustomContentTypeReferences
    {
        public int Id { get; set; }
        public Guid Key { get; set; }
        public string? Alias { get; set; }
        public string? Icon { get; set; }
        public string? Name { get; set; }
        public int? NCCount { get; set; }
       
    }
}
