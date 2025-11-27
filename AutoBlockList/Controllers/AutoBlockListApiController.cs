using NPoco;
using Umbraco.Cms.Core;
using AutoBlockList.Dtos;
using AutoBlockList.Hubs;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Cache;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using AutoBlockList.Services.interfaces;
using Umbraco.Cms.Web.Common.Attributes;
using static Umbraco.Cms.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Infrastructure.Persistence.Querying;

namespace AutoBlockList.Controllers
{
    [IsBackOffice]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class AutoBlockListApiController : UmbracoApiController
    {
        private const string _checkLogs = "Check logs for futher details";
        private AutoBlockListHubClient _client;
        private readonly IScopeProvider _scopeProvider;
        private readonly IAppPolicyCache _runtimeCache;
        private readonly IContentService _contentService;
        private readonly IDataTypeService _dataTypeService;
        private readonly ILogger<AutoBlockListApiController> _logger;
        private readonly IContentTypeService _contentTypeService;
        private readonly IHubContext<AutoBlockListHub> _hubContext;
        private readonly IAutoBlockListService _autoBlockListService;

        public AutoBlockListApiController(IScopeProvider scopeProvider,
            AppCaches appCaches,
            IContentService contentService,
            IDataTypeService dataTypeService,
            ILogger<AutoBlockListApiController> logger,
            IContentTypeService contentTypeService,
            IHubContext<AutoBlockListHub> hubContext,
            IAutoBlockListService autoBlockListService)
        {
            _logger = logger;
            _scopeProvider = scopeProvider;
            _runtimeCache = appCaches.RuntimeCache;
            _contentService = contentService;
            _dataTypeService = dataTypeService;
            _contentTypeService = contentTypeService;
            _hubContext = hubContext;
            _autoBlockListService = autoBlockListService;
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

                    if (contentType != null && !contentTypeReferences.Any(x => x.Id == contentType.Id))
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
        public PagedResult<DisplayAutoBlockListContent> GetAllContentWithNC(int page)
        {
            var contentTypes = _runtimeCache.GetCacheItem(AutoBlockListConstants.CacheKey, () =>
            {
                var contentTypes = GetAllNCContentTypes();

                return contentTypes != null && contentTypes.Any() ? contentTypes : null;
            });

            if (contentTypes == null || !contentTypes.Any())
                return new PagedResult<DisplayAutoBlockListContent>(0, 0, 0);

            var contentTypesIds = contentTypes.Select(x => x.Id).ToList();
            contentTypesIds.AddRange(_autoBlockListService.GetComposedOf(contentTypesIds));

            var filter = new Query<IContent>(_scopeProvider.SqlContext).Where(x => !x.Trashed);
            var items = _contentService.GetPagedOfTypes(contentTypesIds.ToArray(), page, 50, out long totalRecords, filter, null);      
            var result = new PagedResult<DisplayAutoBlockListContent>(totalRecords, page, 50);
            result.Items = items.Select(x => new DisplayAutoBlockListContent()
            {
                ContentType = x.ContentType,
                ContentTypeKey = x.ContentType.Key,
                Name = x.Name,
                Id = x.Id,
                HasBLAssociated = _autoBlockListService.HasBLContent(x)
            });

            return result;
        }

        //Converting
        private ConvertReport ConvertNCDataType(int id)
        {
            var convertReport = new ConvertReport()
            {
                Task = string.Format("Converting NC data type with id {0} to Block list", id),
            };

            try
            {
                IDataType dataType = _dataTypeService.GetDataType(id);
                convertReport.Task = string.Format("Converting '{0}' to Block list", dataType.Name);

                _client.UpdateItem(convertReport.Task);

                var blDataType = _autoBlockListService.CreateBLDataType(dataType);
                var existingDataType = _dataTypeService.GetDataType(blDataType.Name);

                if (blDataType.Name != existingDataType?.Name)
                {
                    _dataTypeService.Save(blDataType);

                    convertReport.Status = AutoBlockListConstants.Status.Success;

                    _client.AddReport(convertReport);
                    return convertReport;
                }

                convertReport.Status = AutoBlockListConstants.Status.Skipped;

                _client.AddReport(convertReport);

                return convertReport;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Format("Failed to convert NC with id '{0}' to block list.", id));

                convertReport.ErrorMessage = _checkLogs;
                convertReport.Status = AutoBlockListConstants.Status.Failed;

                return convertReport;
            }
        }

        [HttpPost]
        public ConvertReport AddDataTypeToContentType(IContentType contentType, IDataType ncDataType)
        {
            var blDataType = _dataTypeService.GetDataType(string.Format(_autoBlockListService.GetNameFormatting(), ncDataType.Name));
            var convertReport = new ConvertReport()
            {
                Task = string.Format("Adding data type '{0}' to document type '{1}'", blDataType.Name, contentType.Name),
                Status = AutoBlockListConstants.Status.Failed
            };

            _client.UpdateItem(convertReport.Task);

            try
            {

                var propertyType = contentType.PropertyTypes.FirstOrDefault(x => x.DataTypeId == ncDataType.Id);
                var isComposition = contentType.CompositionIds().Any();
                
                propertyType = isComposition ? contentType.CompositionPropertyTypes.FirstOrDefault(x => x.DataTypeId == ncDataType.Id) : propertyType;
               
                if (contentType.PropertyTypeExists(string.Format(_autoBlockListService.GetAliasFormatting(), propertyType.Alias)))
                {
                    convertReport.Status = AutoBlockListConstants.Status.Skipped;
                    _client.AddReport(convertReport);
                    return convertReport;
                }

                if (isComposition)
                {
                    var compositionContentTypeIds = contentType.CompositionIds();
                    foreach (var compositionContentTypeId in compositionContentTypeIds)
                    {
                        var compositionContentType = _contentTypeService.Get(compositionContentTypeId);
                        if (compositionContentType != null && compositionContentType.PropertyTypeExists(propertyType.Alias))
                        {
                            if (compositionContentType.PropertyTypeExists(string.Format(_autoBlockListService.GetAliasFormatting(), propertyType.Alias)))
                            {
                                convertReport.Status = AutoBlockListConstants.Status.Skipped;
                                _client.AddReport(convertReport);
                                return convertReport;
                            }

                            compositionContentType.AddPropertyType(_autoBlockListService.MapPropertyType(propertyType, ncDataType, blDataType),
                                    compositionContentType.PropertyGroups.FirstOrDefault(x => x.Id == propertyType.PropertyGroupId.Value).Alias);
                            _contentTypeService.Save(compositionContentType);
                            convertReport.Status = AutoBlockListConstants.Status.Success;

                        }
                    }
                }

                if(contentType.PropertyTypeExists(propertyType.Alias))
                {
                    contentType.AddPropertyType(_autoBlockListService.MapPropertyType(propertyType, ncDataType, blDataType),
                                                contentType.PropertyGroups.FirstOrDefault(x => x.Id == propertyType.PropertyGroupId.Value).Alias);
                    _contentTypeService.Save(contentType);
                    convertReport.Status = AutoBlockListConstants.Status.Success;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add block list to document type");

                convertReport.ErrorMessage = _checkLogs;
            }

            _client.AddReport(convertReport);

            return convertReport;
        }

        private void TransferContent(int id)
        {
            var node = _contentService.GetById(id);
            if (node == null)
            {
                var convertReport = new ConvertReport()
                {
                    Task = "Coverting content",
                    ErrorMessage = string.Format("Failed to find node with id {0}", id),
                    Status = AutoBlockListConstants.Status.Failed
                };

                _client.AddReport(convertReport);
            }

            var allNCProperties = node.Properties.Where(x => x.PropertyType.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent); ;

            foreach (var ncProperty in allNCProperties)
            {
                if (ncProperty.PropertyType.VariesByCulture())
                {
                    foreach (var culture in node.AvailableCultures)
                    {
                        var report = new ConvertReport()
                        {
                            Task = string.Format("Converting '{0}' for culture '{1}' to block list content", ncProperty.PropertyType.Name, culture),
                            Status = AutoBlockListConstants.Status.Failed
                        };

                        _client.UpdateItem(report.Task);

                        try
                        {
                            var value = _autoBlockListService.TransferContent(ncProperty, culture);
                            if (!string.IsNullOrEmpty(value))
                            {
                                node.SetValue(string.Format(_autoBlockListService.GetAliasFormatting(), ncProperty.Alias), value, culture);
                                report.Status = AutoBlockListConstants.Status.Success;
                            }
                            else
                            {
                                report.Status = AutoBlockListConstants.Status.Skipped;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to convert content '{0}' for culture '{1}' to block list", ncProperty.PropertyType.Name);
                            report.ErrorMessage = _checkLogs;
                        }

                        _client.AddReport(report);
                    }
                }
                else
                {
                    var report = new ConvertReport()
                    {
                        Task = string.Format("Converting '{0}' to block list content", ncProperty.PropertyType.Name),
                        Status = AutoBlockListConstants.Status.Failed
                    };

                    _client.UpdateItem(report.Task);

                    try
                    {
                        var value = _autoBlockListService.TransferContent(ncProperty);
                        if (!string.IsNullOrEmpty(value))
                        {
                            node.SetValue(string.Format(_autoBlockListService.GetAliasFormatting(), ncProperty.Alias), value);
                            report.Status = AutoBlockListConstants.Status.Success;
                        }
                        else
                        {
                            report.Status = AutoBlockListConstants.Status.Skipped;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to convert content '{0}' to block list", ncProperty.PropertyType.Name);
                        report.ErrorMessage = _checkLogs;
                    }

                    _client.AddReport(report);
                }
            }

            if (_autoBlockListService.GetSaveAndPublishSetting())
            {
                _contentService.SaveAndPublish(node);
            }
            else
            {
                _contentService.Save(node);
            }
        }

        [HttpPost]
        public IActionResult Convert(ConvertDto dto)
        {
            try
            {
                _client = new AutoBlockListHubClient(_hubContext, dto.ConnectionId);

                foreach (var content in dto.Contents)
                {
                    var index = dto.Contents.FindIndex(x => x == content);

                    _client.CurrentTask("Converting data types");
                    _client.SetTitle(content.Name);
                    _client.SetSubTitle(index);
                    _client.UpdateStep("dataTypes");

                    var fullContentType = _contentTypeService.Get(content.ContentTypeKey);

                    var ncDataTypes = _autoBlockListService.GetDataTypesInContentType(fullContentType).ToArray();

                    foreach (var dataType in ncDataTypes)
                    {
                        var convertReport = ConvertNCDataType(dataType.Id);
                        if (convertReport.Status == AutoBlockListConstants.Status.Failed)
                        {
                            _client.Done(index);
                            return ValidationProblem();
                        }
                    }

                    _client.CurrentTask("Adding data type to document type");
                    _client.UpdateStep("contentTypes");

                    foreach (var dataType in ncDataTypes)
                    {
                        var hasNcDataType = fullContentType.PropertyTypes.FirstOrDefault(x => x.DataTypeId == dataType.Id);
                        hasNcDataType = hasNcDataType == null ? fullContentType.CompositionPropertyTypes.FirstOrDefault(x => x.DataTypeId == dataType.Id) : hasNcDataType;

                        if (hasNcDataType != null)
                        {
                            var convertReport = AddDataTypeToContentType(fullContentType, dataType);
                            if (convertReport.Status == AutoBlockListConstants.Status.Failed)
                            {
                                _client.Done(index);
                                return ValidationProblem();
                            }
                        }
                        else
                        {
                            var contentTypes = _autoBlockListService.GetElementContentTypesFromDataType(dataType);
                            foreach (var contentType in contentTypes)
                            {
                                var convertReport = AddDataTypeToContentType(contentType, dataType);
                                if (convertReport.Status == AutoBlockListConstants.Status.Failed)
                                {
                                    _client.Done(index);
                                    return ValidationProblem();
                                }
                            }
                        }
                    }

                    _client.CurrentTask("Converting content");
                    _client.UpdateStep("content");
                    TransferContent(content.Id);

                    _client.Done(index + 1);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert");
                return ValidationProblem(ex.Message);
            }

            return Ok();
        }
    }
}