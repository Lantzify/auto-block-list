﻿using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AutoBlockList.Dtos.BlockList
{
    public class BlockList
    {
        public BlockListUdi layout { get; set; }
        public List<Dictionary<string, string>> contentData { get; set; }
        public List<Dictionary<string, string>> settingsData { get; set; }
    }
}
