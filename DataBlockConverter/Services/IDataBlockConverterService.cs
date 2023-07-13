using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
using System.Collections.Generic;
using DataBlockConverter.Core.Dtos;
using System.ComponentModel.DataAnnotations;
using static Umbraco.Cms.Core.Constants.Conventions;

namespace DataBlockConverter.Core.Services
{
	public interface IDataBlockConverterService
    {
		IEnumerable<CustomDisplayDataType> GetDataTypesInContentType(IContentType contentType);
        string TransferContent(IProperty property);
        string GetNameFormatting();
        string GetAliasFormatting();
        IDataType? CreateBLDataType(IDataType ncDataType);
		PropertyType? MapPropertyType(IPropertyType propertyType, IDataType ncDataType, IDataType blDataType);
        IEnumerable<CustomDisplayDataType> GetAllNCDataTypes();
		IEnumerable<CustomContentTypeReferences> GetElementContentTypesFromDataType(IDataType dataType);
    }
}
