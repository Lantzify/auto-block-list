using AutoBlockList.Dtos;
using Umbraco.Cms.Core.Models;

namespace AutoBlockList.Services
{
    public interface IAutoBlockListService
    {
        IEnumerable<CustomDisplayDataType> GetDataTypesInContentType(IContentType contentType);
        string TransferContent(IProperty property, string? culture = null);
        string GetNameFormatting();
        string GetAliasFormatting();
        IDataType? CreateBLDataType(IDataType ncDataType);
        PropertyType? MapPropertyType(IPropertyType propertyType, IDataType ncDataType, IDataType blDataType);
        IEnumerable<CustomDisplayDataType> GetAllNCDataTypes();
        IEnumerable<IContentType> GetElementContentTypesFromDataType(IDataType dataType);
    }
}
