using Umbraco.Cms.Core;
using System.Xml.XPath;
using Umbraco.Extensions;
using AutoBlockList.Dtos;
using AutoBlockList.Services;
using Umbraco.Cms.Core.Models;
using AutoBlockList.Constants;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Microsoft.Extensions.Logging;
using static Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Web.Common.Controllers;
using System.ComponentModel.DataAnnotations;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Web.BackOffice.Controllers;
using ObjectTypes = Umbraco.Cms.Core.Models.ObjectTypes;
using static Umbraco.Cms.Core.Models.ContentEditing.DataTypeReferences;

namespace AutoBlockList.Controllers
{
    public class AutoBlockListApiController : UmbracoAuthorizedApiController
    {

        private readonly IContentService _contentService;
        private readonly IDataTypeService _dataTypeService;
        private readonly ILogger<AutoBlockListApiController> _logger;
        private readonly IContentTypeService _contentTypeService;
        private readonly IAutoBlockListService _dataBlockConverterService;

        public AutoBlockListApiController(IContentService contentService,
            IDataTypeService dataTypeService,
            ILogger<AutoBlockListApiController> logger,
            IContentTypeService contentTypeService,
            IAutoBlockListService dataBlockConverterService)
        {
            _logger = logger;
            _contentService = contentService;
            _dataTypeService = dataTypeService;
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

                foreach (var entityType in usages.Where(x => x.Key.EntityType == UmbracoObjectTypes.DocumentType.GetUdiType()))
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
                            IsElement = contentType.IsElement,
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


        //Converting

        public IEnumerable<CustomDisplayDataType> GetDataTypesInContentType(Guid key)
        {
            var contentType = _contentTypeService.Get(key);
            if (contentType == null)
                return null;

            return _dataBlockConverterService.GetDataTypesInContentType(contentType);
        }

        [HttpPost]
        public ConvertReport ConvertNCDataType(ConvertNCDataTypeDto dto)
        {
            var convertReport = new ConvertReport()
            {
                Task = string.Format("Converting NC data type with id {0} to Block list", dto.Id),
            };

            try
            {
                IDataType dataType = _dataTypeService.GetDataType(dto.Id);

                var blDataType = _dataBlockConverterService.CreateBLDataType(dataType);
                var existingDataType = _dataTypeService.GetDataType(blDataType.Name);

                var returnDataType = new CustomDisplayDataType()
                {
                    Icon = blDataType.Editor.Icon,
                    Name = blDataType.Name,
                    MatchingBLId = dto.Id
                };

                if (blDataType.Name != existingDataType?.Name)
                {
                    _dataTypeService.Save(blDataType);

                    returnDataType.Id = blDataType.Id;

                    convertReport.Item = returnDataType;
                    convertReport.Status = AutoBlockListConstants.Status.Success;
                    return convertReport;
                }

                returnDataType.Id = existingDataType.Id;
                returnDataType.Icon = existingDataType.Editor.Icon;
                returnDataType.Name = existingDataType.Name;

                convertReport.Item = returnDataType;
                convertReport.Status = AutoBlockListConstants.Status.Skipped;

                return convertReport;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert  to block list.");

                convertReport.ErrorMessage = ex.Message;
                convertReport.Status = AutoBlockListConstants.Status.Success;

                return convertReport;
            }
        }

        public IEnumerable<CustomContentTypeReferences> GetContentTypesElement(int dataTypeId)
        {
            var elementContentTypes = new List<CustomContentTypeReferences>();

            foreach (var elementContentType in _dataBlockConverterService.GetElementContentTypesFromDataType(_dataTypeService.GetDataType(dataTypeId)))
            {
                elementContentTypes.Add(new CustomContentTypeReferences()
                {
                    Id = elementContentType.Id,
                    Key = elementContentType.Key,
                    Alias = elementContentType.Alias,
                    Icon = elementContentType.Icon,
                    Name = elementContentType.Name,
                    IsElement = elementContentType.IsElement,
                });
            }

            return elementContentTypes;
        }

        [HttpPost]
        public ConvertReport AddDataTypeToContentType(AddDataTypeToContentTypeDto dto)
        {
            var convertReport = new ConvertReport()
            {
                Task = "Adding data type to document type",
                Status = AutoBlockListConstants.Status.Failed
            };

            var contentType = _contentTypeService.Get(dto.ContentTypeId);
            if (contentType == null)
            {
                convertReport.ErrorMessage = "Failed to find doucment type with id " + dto.ContentTypeId.ToString();
                return convertReport;
            }

            var blDataType = _dataTypeService.GetDataType(dto.NewDataTypeId);
            if (blDataType == null)
            {
                convertReport.ErrorMessage = "Failed to find block list data type with id " + dto.ContentTypeId.ToString();
                return convertReport;
            }

            var ncDataType = _dataTypeService.GetDataType(dto.OldDataTypeId);
            if (ncDataType == null)
            {
                convertReport.ErrorMessage = "Failed to find doucment type with id " + dto.ContentTypeId.ToString();
                return convertReport;
            }

            var propertyType = contentType.PropertyTypes.FirstOrDefault(x => x.DataTypeId == dto.OldDataTypeId);


            convertReport.Task = string.Format("Adding data type {0} to document type {1}", blDataType.Name, contentType.Name);
            if (contentType.PropertyTypeExists(string.Format(_dataBlockConverterService.GetAliasFormatting(), propertyType.Alias)))
            {
                convertReport.Status = AutoBlockListConstants.Status.Skipped;
                return convertReport;
            }

            contentType.AddPropertyType(_dataBlockConverterService.MapPropertyType(propertyType, ncDataType, blDataType),
            contentType.PropertyGroups.FirstOrDefault(x => x.Id == propertyType.PropertyGroupId.Value).Alias);

            _contentTypeService.Save(contentType);

            convertReport.Status = AutoBlockListConstants.Status.Success;
            return convertReport;
        }

        [HttpPost]
        public ConvertReport TransferContent(TransferContentDto dto)
        {
            var convertReport = new ConvertReport()
            {
                Task = "Coverting content",
                Status = AutoBlockListConstants.Status.Failed
            };

            var node = _contentService.GetById(dto.ContentId);
            if (node == null)
            {
                convertReport.ErrorMessage = "Failed to find node with id " + dto.ContentId;
                return convertReport;
            }

            var allNCProperties = node.Properties.Where(x => x.PropertyType.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent);

            foreach (var ncProperty in allNCProperties)
            {
                node.SetValue(string.Format(_dataBlockConverterService.GetAliasFormatting(), ncProperty.Alias), _dataBlockConverterService.TransferContent(ncProperty));
            }



            _contentService.Save(node);

            convertReport.Status = AutoBlockListConstants.Status.Success;

            return convertReport;
        }

    }
}