using Umbraco.Cms.Core;
using Umbraco.Extensions;
using AutoBlockList.Dtos;
using AutoBlockList.Services;
using Umbraco.Cms.Core.Models;
using AutoBlockList.Constants;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Services;
using Microsoft.Extensions.Logging;
using static Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Controllers;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Core.Models.ContentEditing;

namespace AutoBlockList.Controllers
{
    [IsBackOffice]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class AutoBlockListApiController : UmbracoApiController
    {
        private const string _checkLogs = "Check logs for futher details";
        private readonly IContentService _contentService;
        private readonly IDataTypeService _dataTypeService;
        private readonly ILogger<AutoBlockListApiController> _logger;
        private readonly IContentTypeService _contentTypeService;
        private readonly ILocalizationService _localizationService;
        private readonly IAutoBlockListService _autoBlockListService;

        public AutoBlockListApiController(IUmbracoMapper umbracoMapper,
            IContentService contentService,
            IDataTypeService dataTypeService,
            ILogger<AutoBlockListApiController> logger,
            IContentTypeService contentTypeService,
             ILocalizationService localizationService,
            IAutoBlockListService autoBlockListService)
        {
            _logger = logger;
            _contentService = contentService;
            _dataTypeService = dataTypeService;
            _contentTypeService = contentTypeService;
            _localizationService = localizationService;
            _autoBlockListService = autoBlockListService;
        }

        [HttpGet]
        public ActionResult<IEnumerable<CustomDisplayDataType>> GetAllNCDataTypes()
        {
            try
            {
                var ncDataTypes = _autoBlockListService.GetAllNCDataTypes();

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

            foreach (var dataType in _autoBlockListService.GetAllNCDataTypes())
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
            var contentTypeIds = GetAllNCContentTypes().Select(x => x.Id).ToArray();
            var items = _contentService.GetPagedOfTypes(contentTypeIds, 0, 100, out long totalRecords, null, null);
            return items;
        }


        //Converting

        public IEnumerable<CustomDisplayDataType> GetDataTypesInContentType(Guid key)
        {
            var contentType = _contentTypeService.Get(key);
            if (contentType == null)
                return null;

            return _autoBlockListService.GetDataTypesInContentType(contentType);
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
                convertReport.Task = string.Format("Converting '{0}' to Block list", dataType.Name);

                var blDataType = _autoBlockListService.CreateBLDataType(dataType);
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
                _logger.LogError(ex, string.Format("Failed to convert NC with id '{0}' to block list.", dto.Id));

                convertReport.ErrorMessage = _checkLogs;
                convertReport.Status = AutoBlockListConstants.Status.Failed;

                return convertReport;
            }
        }

        public IEnumerable<CustomContentTypeReferences> GetContentTypesElement(int dataTypeId)
        {
            var elementContentTypes = new List<CustomContentTypeReferences>();

            foreach (var elementContentType in _autoBlockListService.GetElementContentTypesFromDataType(_dataTypeService.GetDataType(dataTypeId)))
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
                convertReport.ErrorMessage = string.Format("Failed to find doucment type with id '{0}'", dto.ContentTypeId);
                return convertReport;
            }

            var blDataType = _dataTypeService.GetDataType(dto.NewDataTypeId);
            if (blDataType == null)
            {
                convertReport.ErrorMessage = string.Format("Failed to find block list data type with id '{0}'", dto.NewDataTypeId);
                return convertReport;
            }

            var ncDataType = _dataTypeService.GetDataType(dto.OldDataTypeId);
            if (ncDataType == null)
            {
                convertReport.ErrorMessage = string.Format("Failed to find NC data type with id '{0}'", dto.OldDataTypeId);
                return convertReport;
            }

            try
            {
                var propertyType = contentType.PropertyTypes.FirstOrDefault(x => x.DataTypeId == dto.OldDataTypeId);

                convertReport.Task = string.Format("Adding data type '{0}' to document type '{1}'", blDataType.Name, contentType.Name);
                if (contentType.PropertyTypeExists(string.Format(_autoBlockListService.GetAliasFormatting(), propertyType.Alias)))
                {
                    convertReport.Status = AutoBlockListConstants.Status.Skipped;
                    return convertReport;
                }

                contentType.AddPropertyType(_autoBlockListService.MapPropertyType(propertyType, ncDataType, blDataType),
                contentType.PropertyGroups.FirstOrDefault(x => x.Id == propertyType.PropertyGroupId.Value).Alias);
                _contentTypeService.Save(contentType);

                convertReport.Status = AutoBlockListConstants.Status.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Format("Failed to add block list with id '{0}' proprty to document type with id '{1}'", dto.NewDataTypeId, dto.ContentTypeId));

                convertReport.ErrorMessage = _checkLogs;
            }

            return convertReport;
        }

        [HttpPost]
        public IEnumerable<ConvertReport> TransferContent(TransferContentDto dto)
        {
            var convertReports = new List<ConvertReport>();

            var node = _contentService.GetById(dto.ContentId);
            if (node == null)
            {
                convertReports.Add(new ConvertReport()
                {
                    Task = "Coverting content",
                    ErrorMessage = string.Format("Failed to find node with id {0}", dto.ContentId),
                    Status = AutoBlockListConstants.Status.Failed
                });

                return convertReports;
            }

            var allNCProperties = node.Properties.Where(x => x.PropertyType.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent);

            foreach (var ncProperty in allNCProperties)
            {
                if (ncProperty.PropertyType.VariesByCulture())
                {
                    foreach (var culture in _localizationService.GetAllLanguages())
                    {
                        var report = new ConvertReport()
                        {
                            Task = string.Format("Converting '{0}' for culture '{1}' to block list content", ncProperty.PropertyType.Name, culture.CultureName),
                            Status = AutoBlockListConstants.Status.Failed
                        };

                        try
                        {
                            node.SetValue(string.Format(_autoBlockListService.GetAliasFormatting(), ncProperty.Alias), _autoBlockListService.TransferContent(ncProperty, culture.IsoCode), culture.IsoCode);
                            report.Status = AutoBlockListConstants.Status.Success;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to convert content '{0}' for culture '{1}' to block list", ncProperty.PropertyType.Name);
                            report.ErrorMessage = _checkLogs;
                        }

                        convertReports.Add(report);
                    }
                }
                else
                {
                    var report = new ConvertReport()
                    {
                        Task = string.Format("Converting '{0}' to block list content", ncProperty.PropertyType.Name),
                        Status = AutoBlockListConstants.Status.Failed
                    };

                    try
                    {
                        node.SetValue(string.Format(_autoBlockListService.GetAliasFormatting(), ncProperty.Alias), _autoBlockListService.TransferContent(ncProperty));
                        report.Status = AutoBlockListConstants.Status.Success;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to convert content '{0}' to block list", ncProperty.PropertyType.Name);
                        report.ErrorMessage = _checkLogs;
                    }

                    convertReports.Add(report);
                }
            }

            _contentService.Save(node);

            return convertReports;
        }
    }
}