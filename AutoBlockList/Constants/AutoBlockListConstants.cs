namespace AutoBlockList.Constants
{
    public static class AutoBlockListConstants
    {
        public const string CacheKey = "AutoBlockListContentTypes";
        public static readonly string[] DefaultNC = {
            "name",
            "ncContentTypeAlias",
            "PropType",
            "key"
        };

        public const string SQL = @"
SELECT DISTINCT
	n.id AS[nodeId]
FROM
	umbracoNode AS n INNER JOIN
	umbracoDocument AS d ON d.nodeId = n.id INNER JOIN
	umbracoContent AS c ON c.nodeId = n.id INNER JOIN
	umbracoContentVersion AS cv ON cv.nodeId = n.id AND cv.current = 1 INNER JOIN
	cmsPropertyType AS pt ON pt.contentTypeId = c.contentTypeId INNER JOIN
	umbracoPropertyData AS pd ON pd.propertyTypeId = pt.id AND pd.versionId = cv.id
WHERE
	(d.published = 1 OR d.edited = 1) AND
	n.trashed = 0 AND
	pt.Alias in (@propertyTypeIds) AND
	pd.textValue LIKE '%UMBRACO_MACRO%'
ORDER BY
	n.id ASC
;";

		public enum Status
        {
            Success,
            Skipped,
            Failed
        };

    }
}