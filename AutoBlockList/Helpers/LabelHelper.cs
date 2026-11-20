using System.Text.RegularExpressions;

namespace AutoBlockList.Helpers
{
	public static class LabelHelper
	{
		public static string? ToUfm(string? label) => label == null ? null : Regex.Replace(label, @"\{\{\s*(.*?)\s*\}\}", m =>
		{
			var expr = m.Groups[1].Value;

			var ncFilter = Regex.Match(expr, @"^([\w$.]+)\s*\|\s*(ncNodeName|ncRichText)$");
			if (ncFilter.Success)
				return ncFilter.Groups[2].Value == "ncNodeName"
					? $"{{umbContentName: {ncFilter.Groups[1].Value}}}"
					: $"{{={ncFilter.Groups[1].Value} | strip-html}}";

			return Regex.IsMatch(expr, @"^\w+$") ? $"{{={expr}}}" : $"${{ {expr} }}";
		});
	}
}
