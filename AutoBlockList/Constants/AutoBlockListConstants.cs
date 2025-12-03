namespace AutoBlockList.Constants
{
    public static class AutoBlockListConstants
    {
        public const string CheckLogs = "Check logs for futher details";
        public const string CacheKey = "AutoBlockListContentTypes";
        public const string TinyMCECacheKey = "AutoBlockListContentTypesTinyMCE";
        public const string TinyMCECacheKey_Page = "AutoBlockListContentTypesTinyMCE_Page_{0}";

        public static Guid ContentTypeFolderGuid = Guid.Parse("2befed8a-d4a0-43fb-ad34-453cb6c2f63d");

		public static readonly string[] DefaultNC = {
            "name",
            "ncContentTypeAlias",
            "PropType",
            "key"
        };

		public const string SQL_WITH_MACRO_INFO = @"
SELECT
    n.id AS NodeId,
    MAX(CASE 
        WHEN pd.textValue LIKE '%UMBRACO_MACRO%'
        THEN 1 
        ELSE 0 
    END) AS HasMacro
FROM
    umbracoNode AS n 
    INNER JOIN umbracoDocument AS d ON d.nodeId = n.id 
    INNER JOIN umbracoContent AS c ON c.nodeId = n.id 
    INNER JOIN umbracoContentVersion AS cv ON cv.nodeId = n.id AND cv.[current] = 1 
	INNER JOIN umbracoPropertyData AS pd ON pd.versionId = cv.id
	INNER JOIN cmsPropertyType AS pt ON pt.id = pd.propertyTypeId
WHERE
    n.trashed = 0 
    AND (d.published = 1 OR d.edited = 1) 
    AND pt.Alias IN (@propertyTypeIds)
    AND (
        pd.textValue LIKE '%UMBRACO_MACRO%' OR
        pd.textValue LIKE '%umb-rte-block%'
     )
GROUP BY 
    n.id
ORDER BY
    n.id ASC";

		public enum Status
        {
            Success,
            Skipped,
            Failed
        };

    }
}