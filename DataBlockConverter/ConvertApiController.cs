using Umbraco.Cms.Core;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Strings;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using DataBlockConverter.Core.Dtos;
using Microsoft.Extensions.Logging;
using DataBlockConverter.Core.Services;
using static Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Web.Common.Controllers;
using System.ComponentModel.DataAnnotations;
using Umbraco.Cms.Core.Models.ContentEditing;
using DataType = Umbraco.Cms.Core.Models.DataType;
using static Umbraco.Cms.Core.Constants.Conventions;
using ObjectTypes = Umbraco.Cms.Core.Models.ObjectTypes;
using static Umbraco.Cms.Core.Models.ContentEditing.DataTypeReferences;

namespace DataBlockConverter.Core
{
    public class ConvertApiController : UmbracoApiController
    {

        private readonly IContentService _contentService;
        private readonly IDataTypeService _dataTypeService;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly ILogger<ConvertApiController> _logger;
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataBlockConverterService _dataBlockConverterService;

        public ConvertApiController(IContentService contentService,
            IDataTypeService dataTypeService,
            IShortStringHelper shortStringHelper,
            ILogger<ConvertApiController> logger,
            IContentTypeService contentTypeService,
            IDataBlockConverterService dataBlockConverterService)
        {
            _logger = logger;
            _contentService = contentService;
            _dataTypeService = dataTypeService;
            _shortStringHelper = shortStringHelper;
            _contentTypeService = contentTypeService;
            _dataBlockConverterService = dataBlockConverterService;
        }

        [HttpGet]
        public ActionResult<IEnumerable<CustomDisplayDataType>> GetAllNCDataTypes()
        {
            try
            {
                var ncDataTypes = _dataBlockConverterService.GetAllNCDataTypes();

                return ncDataTypes.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retive all nested content data types.");
                return ValidationProblem("Data block converter", "Failed to retive all nested content data types.");
            }
        }

        [HttpGet]
        public IEnumerable<CustomContentTypeReferences> GetAllNCContentTypes()
        {
            var contentTypeReferences = new List<CustomContentTypeReferences>();

            foreach (var dataType in _dataBlockConverterService.GetAllNCDataTypes())
            {
                var result = new DataTypeReferences();
                var usages = _dataTypeService.GetReferences(dataType.Id);

                foreach (var entityType in usages.Where(x => x.Key.EntityType == ObjectTypes.GetUdiType(UmbracoObjectTypes.DocumentType)))
                {
                    var contentType = _contentTypeService.Get(((GuidUdi)entityType.Key).Guid);

                    if (contentType != null)
                        contentTypeReferences.Add(new CustomContentTypeReferences()
                        {
                            Id = contentType.Id,
                            Key = contentType.Key,
                            Alias = contentType.Alias,
                            Icon = contentType.Icon,
                            Name = contentType.Name,
                        });
                }
            }

            return contentTypeReferences;
        }

        [HttpGet]
        public IEnumerable<IContent> GetAllContentWithNC()
        {
            return _contentService.GetPagedOfTypes(GetAllNCContentTypes().Select(x => x.Id).ToArray(), 0, 100, out long totalRecords, null, null);
        }

        [HttpGet]
        public ActionResult ConverNCDataType(int id)
        {
            var dataType = _dataTypeService.GetDataType(id);
            if (dataType == null)
                return null;

            try
            {
                var blDataType = _dataBlockConverterService.CreateBLDataType(dataType);
                 _dataTypeService.Save(blDataType);

                return Ok(blDataType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert {0} to block list.", dataType.Name);
                return ValidationProblem("Failed to convert {0} to block list.", dataType.Name);
            }
        }

        [HttpGet]
        public ActionResult ConvertNCInContentType(int id)
        {
            var contentType = _contentTypeService.Get(id);
            if (contentType == null)
                return null;

            try
            {
                foreach (var propertyGroup in contentType.PropertyGroups)
                {
                    foreach (var propertyType in propertyGroup.PropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent).ToList())
                    {
                        var dataType = _dataTypeService.GetDataType(propertyType.DataTypeId);
                        var blockList = _dataBlockConverterService.CreateBLDataType(dataType);
                        _dataTypeService.Save(blockList);

                        propertyGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, dataType)
                        {
                            DataTypeId = blockList.Id,
                            DataTypeKey = blockList.Key,
                            PropertyEditorAlias = blockList.EditorAlias,
                            ValueStorageType = dataType.DatabaseType,
                            Name = propertyType.Name,
                            Alias = string.Format(_dataBlockConverterService.GetAliasFormatting(), propertyType.Alias),
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

                return Ok(contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert to block list.");
                return ValidationProblem("Failed to convert to block list.");
            }
        }

        [HttpGet]
        public void TransferContent(int id)
        {
            _dataBlockConverterService.TransferContent(id);
        }
    }
}