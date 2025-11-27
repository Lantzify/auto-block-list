using AutoBlockList.Dtos;
using Umbraco.Cms.Core.Models;

namespace AutoBlockList.Services.interfaces
{
    public interface IAutoBlockListService
    {
        IEnumerable<IDataType> GetDataTypesInContentType(IContentType contentType);
        void TransferContent(int id);
		string ConvertPropertyValueToBlockList(IProperty property, string? culture = null);
		string GetNameFormatting();
        string GetAliasFormatting();
        bool GetSaveAndPublishSetting();
        string GetBlockListEditorSize();
        ConvertReport ConvertNCDataType(int id);
        ConvertReport AddDataTypeToContentType(IContentType contentType, IDataType ncDataType);
		IDataType? CreateBLDataType(IDataType ncDataType);
        PropertyType? MapPropertyType(IPropertyType propertyType, IDataType ncDataType, IDataType blDataType);
        IEnumerable<CustomDisplayDataType> GetAllDataTypesWithAlias(string alias);
        IEnumerable<IContentType> GetElementContentTypesFromDataType(IDataType dataType);
        bool HasBLContent(IContent item);
        IEnumerable<IPropertyType> GetPropertyTypes(IContentType contentType);
        IEnumerable<int> GetComposedOf(IEnumerable<int> ids);
    }
}
