using Umbraco.Cms.Core;
using Umbraco.Extensions;
using AutoBlockList.Dtos;
using AutoBlockList.Hubs;
using AutoBlockList.Services;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using AutoBlockList.Constants;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Services;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using static Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Controllers;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Infrastructure.Scoping;
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
		private readonly ILocalizationService _localizationService;
        private readonly IAutoBlockListService _autoBlockListService;

        public AutoBlockListApiController(IScopeProvider scopeProvider,
			AppCaches appCaches,
			IContentService contentService,
            IDataTypeService dataTypeService,
            ILogger<AutoBlockListApiController> logger,
            IContentTypeService contentTypeService,
			IHubContext<AutoBlockListHub> hubContext,

			 ILocalizationService localizationService,
            IAutoBlockListService autoBlockListService)
        {
            _logger = logger;
            _scopeProvider = scopeProvider;
            _runtimeCache = appCaches.RuntimeCache;
            _contentService = contentService;
            _dataTypeService = dataTypeService;
            _contentTypeService = contentTypeService;
            _hubContext = hubContext;
            _localizationService = localizationService;
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
            var contentTypeIds = _runtimeCache.GetCacheItem("AutoBlockListContentTypes", () =>
            {
                var contentTypes = GetAllNCContentTypes();

				return contentTypes != null && contentTypes.Any() ? contentTypes : null;
			})?.Select(x => x.Id).ToArray();
          
			var filter = new Query<IContent>(_scopeProvider.SqlContext).Where(x => !x.Trashed);
			var items = _contentService.GetPagedOfTypes(contentTypeIds, page, 50, out long totalRecords, filter, null);

            var result = new PagedResult<DisplayAutoBlockListContent>(totalRecords, page, 50);
            result.Items = items.Select(x => new DisplayAutoBlockListContent()
            {
                ContentType = x.ContentType,
                ContentTypeKey = x.ContentType.Key,
                Name = x.Name,
                Id = x.Id,
                HasBLAssociated = false
            });

            return result;
        }


        //Converting
        private IDataType ConvertNCDataType(int id)
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
                    return blDataType;
                }

                convertReport.Status = AutoBlockListConstants.Status.Skipped;

				_client.AddReport(convertReport);

				return existingDataType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Format("Failed to convert NC with id '{0}' to block list.", id));

                convertReport.ErrorMessage = _checkLogs;
                convertReport.Status = AutoBlockListConstants.Status.Failed;

                return null;
            }
        }

        [HttpPost]
        public ConvertReport AddDataTypeToContentType(IContentType contentType, IDataType blDataType, IDataType ncDataType)
        {
			var convertReport = new ConvertReport()
			{
				Task = string.Format("Adding data type '{0}' to document type '{1}'", blDataType.Name, contentType.Name),
				Status = AutoBlockListConstants.Status.Failed
			};

			_client.UpdateItem(convertReport.Task);

			try
            {
				var propertyType = contentType.PropertyTypes.FirstOrDefault(x => x.DataTypeId == ncDataType.Id);
				if (contentType.PropertyTypeExists(string.Format(_autoBlockListService.GetAliasFormatting(), propertyType.Alias)))
                {
                    convertReport.Status = AutoBlockListConstants.Status.Skipped;
                    _client.AddReport(convertReport);
                    return convertReport;
                }

                contentType.AddPropertyType(_autoBlockListService.MapPropertyType(propertyType, ncDataType, blDataType),
                contentType.PropertyGroups.FirstOrDefault(x => x.Id == propertyType.PropertyGroupId.Value).Alias);
                _contentTypeService.Save(contentType);

                convertReport.Status = AutoBlockListConstants.Status.Success;
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

				return;
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

						_client.UpdateItem(report.Task);

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
                        node.SetValue(string.Format(_autoBlockListService.GetAliasFormatting(), ncProperty.Alias), _autoBlockListService.TransferContent(ncProperty));
                        report.Status = AutoBlockListConstants.Status.Success;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to convert content '{0}' to block list", ncProperty.PropertyType.Name);
                        report.ErrorMessage = _checkLogs;
                    }

                    _client.AddReport(report);
                }
            }

            _contentService.Save(node);
        }

        [HttpPost]
        public void Convert(ConvertDto dto)
        {
			_client = new AutoBlockListHubClient(_hubContext, dto.ConnectionId);

            foreach (var content in dto.Contents)
            {
				_client.UpdateTask("dataTypes");

                var fullContentType = _contentTypeService.Get(content.ContentTypeKey);
                var ncDataTypes = _dataTypeService.GetAll(_autoBlockListService.GetDataTypesInContentType(fullContentType).Select(x => x.Id).ToArray()).ToArray();

                var blDataTypes = new List<IDataType>();

                foreach (var dataType in ncDataTypes)
                {
                    var blDataType = ConvertNCDataType(dataType.Id);
                    if (blDataType.Name != null)
                    {
                        blDataTypes.Add(blDataType);
                    }
                }

                if (blDataTypes != null && blDataTypes.Any())
                {
                    _client.UpdateTask("contentTypes");

					var contentTypes = new List<IContentType>
					{
						fullContentType
					};


                    //FIX THIS FOR NESTLING
					//contentTypes.AddRange(_autoBlockListService.GetElementContentTypesFromDataType(1));

                    for (int i = 0; i < ncDataTypes.Count(); i++)
                    {

						AddDataTypeToContentType(fullContentType, blDataTypes[i], ncDataTypes[i]);
                    }

                    _client.UpdateTask("content");
                    TransferContent(content.Id);


                    _client.Done()
                }
            }
        }
    }
}