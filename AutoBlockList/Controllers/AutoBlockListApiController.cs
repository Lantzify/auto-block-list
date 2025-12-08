using NPoco;
using System.Linq;
using Newtonsoft.Json;
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
using AutoBlockList.Dtos.BlockList;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.PropertyEditors;
using AutoBlockList.Services.interfaces;
using Umbraco.Cms.Web.Common.Attributes;
using static Umbraco.Cms.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.Authorization;
using System.ComponentModel.DataAnnotations;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Infrastructure.Persistence.Querying;

namespace AutoBlockList.Controllers
{
	[IsBackOffice]
	[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
	public class AutoBlockListApiController : UmbracoApiController
	{
		private readonly IScopeProvider _scopeProvider;
		private readonly IAppPolicyCache _runtimeCache;
		private readonly IContentService _contentService;
		private readonly IDataTypeService _dataTypeService;
		private readonly ILogger<AutoBlockListApiController> _logger;
		private readonly IContentTypeService _contentTypeService;
		private readonly IAutoBlockListService _autoBlockListService;
		private readonly IAutoBlockListMacroService _autoBlockListMacroService;
		private readonly IAutoBlockListContext _autoBlockListContext;
		private readonly IAutoBlockListHubClientFactory _autoBlockListHubClientFactory;

		public AutoBlockListApiController(IScopeProvider scopeProvider,
			AppCaches appCaches,
			IContentService contentService,
			IProfilingLogger profilingLogger,
			IDataTypeService dataTypeService,
			ILogger<AutoBlockListApiController> logger,
			IContentTypeService contentTypeService,
			IHubContext<AutoBlockListHub> hubContext,
			IAutoBlockListService autoBlockListService,
			IAutoBlockListMacroService autoBlockListMacroService,
			IAutoBlockListContext autoBlockListContext,
			IAutoBlockListHubClientFactory autoBlockListHubClientFactory)
		{
			_logger = logger;
			_scopeProvider = scopeProvider;
			_runtimeCache = appCaches.RuntimeCache;
			_contentService = contentService;
			_dataTypeService = dataTypeService;
			_contentTypeService = contentTypeService;
			_autoBlockListService = autoBlockListService;
			_autoBlockListMacroService = autoBlockListMacroService;
			_autoBlockListContext = autoBlockListContext;
			_autoBlockListHubClientFactory = autoBlockListHubClientFactory;
		}

		[HttpGet]
		public IEnumerable<CustomContentTypeReferences> GetContentTypesByPropertyEditorAlias(string alias)
		{
			var contentTypeReferences = new List<CustomContentTypeReferences>();

			foreach (var dataType in _autoBlockListService.GetAllDataTypesWithAlias(alias))
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



		#region Macros
		[HttpGet]
		public PagedResult<DisplayAutoBlockListContent> GetAllContentWithTinyMce(int page)
		{
			var contentTypes = _runtimeCache.GetCacheItem(AutoBlockListConstants.TinyMCECacheKey, () =>
			{
				var contentTypes = GetContentTypesByPropertyEditorAlias(PropertyEditors.Aliases.TinyMce).ToList();
				contentTypes.AddRange(GetContentTypesByPropertyEditorAlias(PropertyEditors.Aliases.BlockList));

				return contentTypes != null && contentTypes.Any() ? contentTypes : null;
			});

			if (contentTypes == null || !contentTypes.Any())
				return new PagedResult<DisplayAutoBlockListContent>(0, 0, 0);

			var contentTypesIds = contentTypes.Select(x => x.Id).ToList();
			contentTypesIds.AddRange(_autoBlockListService.GetComposedOf(contentTypesIds));

			var tinyMcePropertyTypeIds = new List<string>();
			foreach (var contentTypeId in contentTypesIds)
			{
				var contentType = _contentTypeService.Get(contentTypeId);
				var tinyMceProperties = contentType.PropertyTypes.Where(x => AutoBlockListConstants.RichTextEditor_And_BlockListAlias.Contains(x.PropertyEditorAlias));
				tinyMcePropertyTypeIds.AddRange(tinyMceProperties.Select(p => p.Alias));
			}

			if (!tinyMcePropertyTypeIds.Any())
				return new PagedResult<DisplayAutoBlockListContent>(0, 0, 0);

			tinyMcePropertyTypeIds = tinyMcePropertyTypeIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

			using (var scope = _scopeProvider.CreateScope())
			{
				Page<ContentWithMacroInfo> pagedResult = _runtimeCache.GetCacheItem(string.Format(AutoBlockListConstants.TinyMCECacheKey_Page, page), () =>
				{
					var pagedResult =  scope.Database.Page<ContentWithMacroInfo>(page, 15, AutoBlockListConstants.SQL_WITH_MACRO_INFO, new { propertyTypeIds = tinyMcePropertyTypeIds });

					return pagedResult.Items.Any() ? pagedResult : null;
				});

				if (pagedResult == null)
					return new PagedResult<DisplayAutoBlockListContent>(0, 0, 0);

				var nodeIds = pagedResult.Items.Select(x => x.NodeId).Distinct().ToArray();
				var contentItems = _contentService.GetByIds(nodeIds).ToDictionary(c => c.Id);

				var displayItems = pagedResult.Items
					.Where(x => contentItems.ContainsKey(x.NodeId))
					.Select(x =>
					{
						var content = contentItems[x.NodeId];
						return new DisplayAutoBlockListContent()
						{
							ContentType = content.ContentType,
							ContentTypeKey = content.ContentType.Key,
							Name = content.Name,
							Id = content.Id,
							HasBLAssociated = x.HasMacro
						};
					})
					.ToList();

				var result = new PagedResult<DisplayAutoBlockListContent>(pagedResult.TotalItems, pagedResult.CurrentPage, pagedResult.ItemsPerPage)
				{
					Items = displayItems
				};

				scope.Complete();
				return result;
			}
		}

		[HttpPost]
		public IActionResult ConvertMacro(ConvertDto dto)
		{
			var contentMacroReport = new ConvertReport();

			var client = _autoBlockListHubClientFactory.CreateClient(dto.ConnectionId);
			_autoBlockListContext.SetClient(client);

			try
			{
				foreach (var autoBlockListContent in dto.Contents)
				{
					var content = _contentService.GetById(autoBlockListContent.Id);
					var fullContentType = _contentTypeService.Get(autoBlockListContent.ContentTypeKey);
					var index = dto.Contents.FindIndex(x => x == autoBlockListContent);

					contentMacroReport = new ConvertReport()
					{
						Task = $"Converting macros on page: '{content.Name}'",
					};

					client.SetTitle(content.Name);
					client.SetSubTitle(index);
					client.CurrentTask(contentMacroReport.Task);

					bool shouldSave = false;		

					var tinyMcePropertyTypes = fullContentType.PropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.TinyMce).ToList();
					tinyMcePropertyTypes.AddRange(fullContentType.CompositionPropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.TinyMce));
					tinyMcePropertyTypes.DistinctBy(x => x.Key);

					foreach (var tinyMceDataType in tinyMcePropertyTypes)
					{
						var tinyMceDataTypeReport = new ConvertReport()
						{
							Task = $"Checking for macros in rich text editor with name: '{tinyMceDataType.Name}'",
						};

						client.CurrentTask(tinyMceDataTypeReport.Task);

						if (tinyMceDataType.VariesByCulture())
						{
							foreach (var culture in content.AvailableCultures)
							{
								tinyMceDataTypeReport.Task += $" for culture: '{culture}'";
								if (_autoBlockListMacroService.ProcessContentForMacroConversion(content, tinyMceDataType, culture))
									shouldSave = true;
							}
						}
						else
						{
							var tinyMceContent = content.GetValue<string>(tinyMceDataType.Alias);
							if (_autoBlockListMacroService.ProcessContentForMacroConversion(content, tinyMceDataType))
								shouldSave = true;
						}
					}

					var blockListPropertyTypes = fullContentType.PropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.BlockList).ToList();
					blockListPropertyTypes.AddRange(fullContentType.CompositionPropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.BlockList));

					if (blockListPropertyTypes != null && blockListPropertyTypes.Any())
					{
						var contentTypes = _runtimeCache.GetCacheItem<IEnumerable<CustomContentTypeReferences>>(AutoBlockListConstants.TinyMCECacheKey);
						var contentTypeAliases = contentTypes.Select(x => x.Alias);
						var contentTypeKeys = contentTypes.Select(s => s.Key);

						foreach (var blockListPropertyType in blockListPropertyTypes)
						{
							var blocklistDataTypeReport = new ConvertReport()
							{
								Task = $"Checking for macros in block list with name: '{blockListPropertyType.Name}'",
							};

							client.CurrentTask(blocklistDataTypeReport.Task);

							if (blockListPropertyType.VariesByCulture())
							{
								foreach (var culture in content.AvailableCultures)
								{
									blocklistDataTypeReport.Task += $" for culture: '{culture}'";

									var dataType = _dataTypeService.GetDataType(blockListPropertyType.DataTypeId);
									var blockListConfig = dataType.Configuration as BlockListConfiguration;

									var stringValue = content.GetValue<string>(blockListPropertyType.Alias, culture);
									var processedBlocklistValue = _autoBlockListMacroService.ProcessBlockListValues(stringValue, contentTypeKeys);

									if (processedBlocklistValue != stringValue)
									{
										shouldSave = true;
										content.SetValue(blockListPropertyType.Alias, processedBlocklistValue, culture);
									}
								}
							}
							else
							{
								var dataType = _dataTypeService.GetDataType(blockListPropertyType.DataTypeId);
								var blockListConfig = dataType.Configuration as BlockListConfiguration;

								var stringValue = content.GetValue<string>(blockListPropertyType.Alias);
								var processedBlocklistValue = _autoBlockListMacroService.ProcessBlockListValues(stringValue, contentTypeKeys);

								if (processedBlocklistValue != stringValue)
								{
									shouldSave = true;
									content.SetValue(blockListPropertyType.Alias, processedBlocklistValue);
								}
							}
						}
					}

					if (!shouldSave)
					{
						contentMacroReport.Status = AutoBlockListConstants.Status.Skipped;
						client.AddReport(contentMacroReport);
						client.Done(index + 1);
						continue;
					}

					if (_autoBlockListService.GetSaveAndPublishSetting())
					{
						client.CurrentTask("Saving and publishing node: " + content.Name); 
						_contentService.SaveAndPublish(content);
						client.AddReport(new ConvertReport()
						{
							Task = $"Saving and publishing content: '{content.Name}'",
							Status = AutoBlockListConstants.Status.Success
						});
					}
					else
					{
						client.CurrentTask("Saving node: " + content.Name);
						_contentService.Save(content);
						client.AddReport(new ConvertReport()
						{
							Task = $"Saving content: {content.Name}",
							Status = AutoBlockListConstants.Status.Success
						});
					}

					client.AddReport(contentMacroReport);
					client.Done(index + 1);
				}	
			}
			catch (Exception ex)
			{
				contentMacroReport.Status = AutoBlockListConstants.Status.Failed;
				client.AddReport(contentMacroReport);
				client.Done("failed");

				_logger.LogError(ex, $"Failed to: {contentMacroReport.Task}");
				_autoBlockListContext.ClearClient();
				return ValidationProblem(ex.Message);
			}

			_autoBlockListContext.ClearClient();
			return Ok();
		}

		#endregion

		#region NestedContent
		[HttpGet]
		public PagedResult<DisplayAutoBlockListContent> GetAllContentWithNC(int page)
		{
			var contentTypes = _runtimeCache.GetCacheItem(AutoBlockListConstants.CacheKey, () =>
			{
				var contentTypes = GetContentTypesByPropertyEditorAlias(PropertyEditors.Aliases.NestedContent);

				return contentTypes != null && contentTypes.Any() ? contentTypes : null;
			});

			if (contentTypes == null || !contentTypes.Any())
                return new PagedResult<DisplayAutoBlockListContent>(0, 0, 0);

			var contentTypesIds = contentTypes.Select(x => x.Id).ToList();
			contentTypesIds.AddRange(_autoBlockListService.GetComposedOf(contentTypesIds));

			var filter = new Query<IContent>(_scopeProvider.SqlContext).Where(x => !x.Trashed);
			var items = _contentService.GetPagedOfTypes(contentTypesIds.ToArray(), page, 15, out long totalRecords, filter, null);
			var result = new PagedResult<DisplayAutoBlockListContent>(totalRecords, page, 15);
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


		[HttpPost]
		public IActionResult ConvertNC(ConvertDto dto)
		{
			var client = _autoBlockListHubClientFactory.CreateClient(dto.ConnectionId);
			_autoBlockListContext.SetClient(client);

			try
			{
				foreach (var content in dto.Contents)
				{
					var index = dto.Contents.FindIndex(x => x == content);

					client.CurrentTask("Converting data types");
					client.SetTitle(content.Name);
					client.SetSubTitle(index);
					client.UpdateStep("dataTypes");

					var fullContentType = _contentTypeService.Get(content.ContentTypeKey);

					var ncDataTypes = _autoBlockListService.GetDataTypesInContentType(fullContentType).ToArray();

					foreach (var dataType in ncDataTypes)
					{
						var convertReport = _autoBlockListService.ConvertNCDataType(dataType.Id);
						if (convertReport.Status == AutoBlockListConstants.Status.Failed)
						{
							client.Done(index);
							return ValidationProblem();
						}
					}

					client.CurrentTask("Adding data type to document type");
					client.UpdateStep("contentTypes");

					foreach (var dataType in ncDataTypes)
					{
						var hasNcDataType = fullContentType.PropertyTypes.FirstOrDefault(x => x.DataTypeId == dataType.Id);
						hasNcDataType = hasNcDataType == null ? fullContentType.CompositionPropertyTypes.FirstOrDefault(x => x.DataTypeId == dataType.Id) : hasNcDataType;

						if (hasNcDataType != null)
						{
							var convertReport = _autoBlockListService.AddDataTypeToContentType(fullContentType, dataType);
							if (convertReport.Status == AutoBlockListConstants.Status.Failed)
							{
								client.Done(index);
								return ValidationProblem();
							}
						}
						else
						{
							var contentTypes = _autoBlockListService.GetElementContentTypesFromDataType(dataType);
							foreach (var contentType in contentTypes)
							{
								var convertReport = _autoBlockListService.AddDataTypeToContentType(contentType, dataType);
								if (convertReport.Status == AutoBlockListConstants.Status.Failed)
								{
									client.Done(index);
									return ValidationProblem();
								}
							}
						}
					}

					client.CurrentTask("Converting content");
					client.UpdateStep("content");
					_autoBlockListService.TransferContent(content.Id);

					client.Done(index + 1);
				}

			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to convert");
				_autoBlockListContext.ClearClient();
				return ValidationProblem(ex.Message);
			}

			_autoBlockListContext.ClearClient();
			return Ok();
		}
		#endregion
	}
}