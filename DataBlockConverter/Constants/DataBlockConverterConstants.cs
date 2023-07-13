using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DataBlockConverter.Constants
{
	public static class DataBlockConverterConstants
	{
		public static readonly string[] DefaultNC = {
			"name",
			"ncContentTypeAlias",
			"PropType",
			"key"
		};

		public enum Status {
			Success,
			Skipped,
			Failed
		};

	}
}
