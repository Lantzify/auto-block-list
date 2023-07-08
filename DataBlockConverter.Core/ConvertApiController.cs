using Umbraco.Cms.Core;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Strings;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using System.Collections.Generic;
using DataBlockConverter.Core.Dtos;
using DataBlockConverter.Core.Services;
using static Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Models.ContentEditing;
using static Umbraco.Cms.Core.Constants.Conventions;
using ObjectTypes = Umbraco.Cms.Core.Models.ObjectTypes;

namespace DataBlockConverter.Core
{
    public class ConvertApiController : UmbracoApiController
    {
        private readonly ITrackedReferencesService _trackedReferencesService;
        private readonly IDataBlockConverterService _dataBlockConverterService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IDataTypeService _dataTypeService;


        public ConvertApiController(IDataBlockConverterService dataBlockConverterService,
            IShortStringHelper shortStringHelper,
           IContentTypeService contentTypeService,
           IDataTypeService dataTypeService,
           ITrackedReferencesService trackedReferencesService)
        {
            _shortStringHelper = shortStringHelper;
            _dataBlockConverterService = dataBlockConverterService;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
            _trackedReferencesService = trackedReferencesService;
        }

        public IEnumerable<CustomDisplayDataType> GetAllNCDataTypes()
        {
            var dataTypes = new List<CustomDisplayDataType>();
            foreach (var dataType in _dataTypeService.GetAll().Where(x => x.EditorAlias == PropertyEditors.Aliases.NestedContent))
                dataTypes.Add(new CustomDisplayDataType()
                {
                    Id = dataType.Id,
                    Name = dataType.Name,
                    Icon = dataType.Editor.Icon
                });

           return  dataTypes;
        }

        public List<DataTypeReferences> GetAllNCContentTypes()
        {
            var dataTypes = GetAllNCDataTypes();
            var foo = new List<DataTypeReferences>();

            //Make this code better

            foreach (var dataType in dataTypes)
            {
                var result = new DataTypeReferences();
                var usages = _dataTypeService.GetReferences(dataType.Id);

                foreach (var groupOfEntityType in usages.GroupBy(x => x.Key.EntityType))
                {
                    //get all the GUIDs for the content types to find
                    var guidsAndPropertyAliases = groupOfEntityType.ToDictionary(i => ((GuidUdi)i.Key).Guid, i => i.Value);

                    if (groupOfEntityType.Key == ObjectTypes.GetUdiType(UmbracoObjectTypes.DocumentType))
                        result.DocumentTypes = GetContentTypeUsages(_contentTypeService.GetAll(guidsAndPropertyAliases.Keys), guidsAndPropertyAliases);
                }

                foo.Add(result);
            }

            return foo;
        }

        private IEnumerable<DataTypeReferences.ContentTypeReferences> GetContentTypeUsages(
    IEnumerable<IContentTypeBase> cts,
    IReadOnlyDictionary<Guid, IEnumerable<string>> usages)
        {
            return cts.Select(x => new DataTypeReferences.ContentTypeReferences
            {
                Id = x.Id,
                Key = x.Key,
                Alias = x.Alias,
                Icon = x.Icon,
                Name = x.Name,
                Udi = new GuidUdi(ObjectTypes.GetUdiType(UmbracoObjectTypes.DocumentType), x.Key),
                //only select matching properties
                Properties = x.PropertyTypes.Where(p => usages[x.Key].InvariantContains(p.Alias))
                    .Select(p => new DataTypeReferences.ContentTypeReferences.PropertyTypeReferences
                    {
                        Alias = p.Alias,
                        Name = p.Name
                    })
            });
        }

        public void ConverNCDataType(int id)
        {
            var dataType = _dataTypeService.GetDataType(id);
            if (dataType == null)
                return;

            _dataTypeService.Save(_dataBlockConverterService.CreateBLDataType(dataType));
        }

        //https://localhost:44391/umbraco/api/ConvertApi/ConvertNCInContentType?id=1058
        [HttpGet]
        public void ConvertNCInContentType(int id)
        {
            var contentType = _contentTypeService.Get(id);
            if (contentType == null)
                return;

            foreach (var propertyGroup in contentType.PropertyGroups)
            {
                foreach (var propertyType in propertyGroup.PropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent))
                {
                    var dataType = _dataTypeService.GetDataType(propertyType.DataTypeId);
                    _dataTypeService.Save(_dataBlockConverterService.CreateBLDataType(dataType));

                    propertyGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, dataType)
                    {
                        DataTypeId = dataType.Id,
                        DataTypeKey = dataType.Key,
                        PropertyEditorAlias = dataType.EditorAlias,
                        ValueStorageType = dataType.DatabaseType,
                        Name = propertyType.Name,
                        Alias = propertyType.Alias + "BL",
                        CreateDate = DateTime.Now,
                        Description = propertyType.Description,
                        Mandatory = propertyType.Mandatory,
                        MandatoryMessage = propertyType.MandatoryMessage,
                        ValidationRegExp = propertyType.ValidationRegExp,
                        ValidationRegExpMessage = propertyType.ValidationRegExpMessage,
                        Variations = propertyType.Variations,
                        LabelOnTop = propertyType.LabelOnTop,
                        PropertyGroupId = propertyType.PropertyGroupId,
                        SupportsPublishing = propertyType.SupportsPublishing,
                        SortOrder = propertyType.SortOrder,
                    });
                }
            }
            _contentTypeService.Save(contentType);
        }
    }
}