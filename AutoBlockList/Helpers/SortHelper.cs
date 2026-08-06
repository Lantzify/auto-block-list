using Umbraco.Cms.Core.Models;

namespace AutoBlockList.Helpers
{
	public static class SortHelper
	{
		public static bool InsertPropertyTypeAfter(IContentTypeBase contentType, 
			PropertyGroup propertyGroup, 
			string afterAlias,
			PropertyType propertyType)
		{
			var existing = propertyGroup.PropertyTypes?.OrderBy(x => x.SortOrder).ToList()	?? new List<IPropertyType>();
			var after = existing.FirstOrDefault(x => x.Alias == afterAlias);

			var sortOrder = after != null
			 ? after.SortOrder + 1
			 : (existing.Any() ? existing.Max(x => x.SortOrder) + 1 : 0);

			foreach (var propertyTypeToMove in existing.Where(x => x.SortOrder >= sortOrder))
				propertyTypeToMove.SortOrder++;

			propertyType.SortOrder = sortOrder;

			return contentType.AddPropertyType(propertyType, propertyGroup.Alias);
		}
	}
}