using Umbraco.Cms.Core;
using AutoBlockList.Dtos;
using AutoBlockList.Hubs;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Cache;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Web.Common.Attributes;
using static Umbraco.Cms.Core.Constants;
using AutoBlockList.Services.interfaces;
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
		private readonly IMacroService _macroService;
		private readonly IScopeProvider _scopeProvider;
		private readonly IAppPolicyCache _runtimeCache;
		private readonly IJsonSerializer _jsonSerializer;
		private readonly IContentService _contentService;
		private readonly IDataTypeService _dataTypeService;
		private readonly ILogger<AutoBlockListApiController> _logger;
		private readonly IContentTypeService _contentTypeService;
		private readonly IHubContext<AutoBlockListHub> _hubContext;
		private readonly IAutoBlockListService _autoBlockListService;
		private readonly IAutoBlockListMacroService _autoBlockListMacroService;

		public AutoBlockListApiController(IMacroService macroService,
			IScopeProvider scopeProvider,
			AppCaches appCaches,
			IJsonSerializer jsonSerializer,
			IContentService contentService,
			IDataTypeService dataTypeService,
			ILogger<AutoBlockListApiController> logger,
			IContentTypeService contentTypeService,
			IHubContext<AutoBlockListHub> hubContext,
			IAutoBlockListService autoBlockListService,
			IAutoBlockListMacroService autoBlockListMacroService)
		{
			_logger = logger;
			_macroService = macroService;
			_scopeProvider = scopeProvider;
			_runtimeCache = appCaches.RuntimeCache;
			_jsonSerializer = jsonSerializer;
			_contentService = contentService;
			_dataTypeService = dataTypeService;
			_contentTypeService = contentTypeService;
			_hubContext = hubContext;
			_autoBlockListService = autoBlockListService;
			_autoBlockListMacroService = autoBlockListMacroService;
		}

		[HttpGet]
		public IEnumerable<CustomContentTypeReferences> GetAllNCContentTypes(string alias)
		{
			var contentTypeReferences = new List<CustomContentTypeReferences>();

			foreach (var dataType in _autoBlockListService.GetAllNCDataTypes(alias))
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
			var contentTypes = GetAllNCContentTypes(PropertyEditors.Aliases.TinyMce);
			var contentTypesIds = contentTypes.Select(x => x.Id).ToList();
			contentTypesIds.AddRange(_autoBlockListService.GetComposedOf(contentTypesIds));

			var tinyMcePropertyTypeIds = new List<string>();
			foreach (var contentTypeId in contentTypesIds)
			{
				var contentType = _contentTypeService.Get(contentTypeId);
				var tinyMceProperties = contentType.PropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.TinyMce);
				tinyMcePropertyTypeIds.AddRange(tinyMceProperties.Select(p => p.Alias));
			}

			if (!tinyMcePropertyTypeIds.Any())
			{
				return new PagedResult<DisplayAutoBlockListContent>(0, page, 50);
			}

			using (var scope = _scopeProvider.CreateScope())
			{
				var contentIdsWithMacros = scope.Database.Fetch<ContentWithMacroInfo>(AutoBlockListConstants.SQL_WITH_MACRO_INFO, new { propertyTypeIds = tinyMcePropertyTypeIds });

				var totalRecords = contentIdsWithMacros.Count;

				var pageSize = 50;
				var skip = page * pageSize;
				var pagedContentIds = contentIdsWithMacros.Skip(skip).Take(pageSize).ToList();

				var items = pagedContentIds.Select(x => new
				{
					Content = _contentService.GetById(x.NodeId),
					HasMacro = x.HasMacro,

				})
					.Where(x => x.Content != null)
					.ToList();

				var result = new PagedResult<DisplayAutoBlockListContent>(totalRecords, page, pageSize);
				result.Items = items.Select(x => new DisplayAutoBlockListContent()
				{
					ContentType = x.Content.ContentType,
					ContentTypeKey = x.Content.ContentType.Key,
					Name = x.Content.Name,
					Id = x.Content.Id,
					HasBLAssociated = x.HasMacro
				});

				scope.Complete();
				return result;
			}
		}

		[HttpPost]
		public async Task<IActionResult> ConvertMacro(ConvertDto dto)
		{
			try
			{
				_client = new AutoBlockListHubClient(_hubContext, dto.ConnectionId);

				foreach (var autoBlockListContent in dto.Contents)
				{
					var content = _contentService.GetById(autoBlockListContent.Id);
					var fullContentType = _contentTypeService.Get(autoBlockListContent.ContentTypeKey);
					var index = dto.Contents.FindIndex(x => x == autoBlockListContent);


					var contentMacroReport = new ConvertReport()
					{
						Task = $"Converting macros on page: '{content.Name}'",
					};

					await _client.SetTitle(content.Name);
					await _client.SetSubTitle(index);
					await _client.CurrentTask(contentMacroReport.Task);

					bool shouldSave = true;

					var tinyMceDataTypes = fullContentType.PropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.TinyMce);
					foreach (var tinyMceDataType in tinyMceDataTypes)
					{
						var tinyMceDataTypeReport = new ConvertReport()
						{
							Task = $"Checking for macros in rich text editor with name: '{tinyMceDataType.Name}'",
						};

						await _client.CurrentTask(tinyMceDataTypeReport.Task);

						bool hasChanges = false;
						if (tinyMceDataType.VariesByCulture())
						{
							foreach (var culture in content.AvailableCultures)
							{
								tinyMceDataTypeReport.Task += $" for culture: '{culture}'";
								var tinyMceContent = content.GetValue<string>(tinyMceDataType.Alias, culture);

								if (!string.IsNullOrEmpty(tinyMceContent) && _autoBlockListMacroService.HasMacro(tinyMceContent))
								{
									RichTextPropertyEditorHelper.TryParseRichTextEditorValue(tinyMceContent, _jsonSerializer, _logger, out var richTextEditorValue);
									var macros = _autoBlockListMacroService.GetMacroStrings(richTextEditorValue.Markup);

									foreach (var macroString in macros)
									{
										var macroStringReport = new ConvertReport
										{
											Task = $"Converting macro: '{macroString}' to document type with culture: '{culture}'",
											Status = AutoBlockListConstants.Status.Success
										};
										await _client.CurrentTask(macroStringReport.Task);

										var parameters = _autoBlockListMacroService.GetParametersFromMaco(macroString);
										if (string.IsNullOrEmpty(parameters["macroAlias"].ToString()))
										{
											macroStringReport.Status = AutoBlockListConstants.Status.Failed;
											await _client.AddReport(macroStringReport);
											continue;
										}

										var macro = _macroService.GetByAlias(parameters["macroAlias"].ToString());
										if (macro == null)
										{
											macroStringReport.Status = AutoBlockListConstants.Status.Failed;
											await _client.AddReport(macroStringReport);
											continue;
										}

										var contentType = _autoBlockListMacroService.ConvertMacroToContentType(macro, out List<ConvertReport> reports);
										foreach (var report in reports)
											await _client.AddReport(report);

										await _client.AddReport(macroStringReport);

										var dataConvertReport = new ConvertReport
										{
											Task = $"Converting macro with to block list: '{contentType.Name}' for culture: '{culture}'",
											Status = AutoBlockListConstants.Status.Success
										};

										await _client.CurrentTask(dataConvertReport.Task);

										var dataType = _dataTypeService.GetDataType(tinyMceDataType.DataTypeId);
										var editor = dataType.Editor;
										if (editor?.GetConfigurationEditor() is not RichTextConfigurationEditor configEditor)
										{
											dataConvertReport.Status = AutoBlockListConstants.Status.Failed;
											await _client.AddReport(dataConvertReport);
											continue;
										}

										await _client.AddReport(dataConvertReport);

										richTextEditorValue = _autoBlockListMacroService.ReplaceMacroWithBlockList(macroString, parameters, configEditor, dataType, contentType, richTextEditorValue);
										hasChanges = true;

										var createPartialViewReport = _autoBlockListMacroService.CreatePartialView(macro);
										await _client.AddReport(createPartialViewReport);
									}

									if (hasChanges)
										content.SetValue(tinyMceDataType.Alias, RichTextPropertyEditorHelper.SerializeRichTextEditorValue(richTextEditorValue, _jsonSerializer), culture);
								}
								else
								{
									tinyMceDataTypeReport.Status = AutoBlockListConstants.Status.Skipped;
									await _client.AddReport(tinyMceDataTypeReport);
									continue;
								}
							}
						}
						else
						{
							var tinyMceContent = content.GetValue<string>(tinyMceDataType.Alias);

							if (!string.IsNullOrEmpty(tinyMceContent) && _autoBlockListMacroService.HasMacro(tinyMceContent))
							{
								RichTextPropertyEditorHelper.TryParseRichTextEditorValue(tinyMceContent, _jsonSerializer, _logger, out var richTextEditorValue);
								var macros = _autoBlockListMacroService.GetMacroStrings(richTextEditorValue.Markup);

								foreach (var macroString in macros)
								{
									var macroStringReport = new ConvertReport
									{
										Task = $"Converting macro: '{macroString}' to document type",
										Status = AutoBlockListConstants.Status.Success
									};
									await _client.CurrentTask(macroStringReport.Task);

									var parameters = _autoBlockListMacroService.GetParametersFromMaco(macroString);
									if (string.IsNullOrEmpty(parameters["macroAlias"].ToString()))
									{
										macroStringReport.Status = AutoBlockListConstants.Status.Failed;
										macroStringReport.ErrorMessage = "Macro alias is missing";
										await _client.AddReport(macroStringReport);
										continue;
									}

									var macro = _macroService.GetByAlias(parameters["macroAlias"].ToString());
									if (macro == null)
									{
										macroStringReport.Status = AutoBlockListConstants.Status.Failed;
										macroStringReport.ErrorMessage = "No macro found";
										await _client.AddReport(macroStringReport);
										continue;
									}

									var contentType = _autoBlockListMacroService.ConvertMacroToContentType(macro, out List<ConvertReport> reports);
									foreach (var report in reports)
										await _client.AddReport(report);

									await _client.AddReport(macroStringReport);

									var dataConvertReport = new ConvertReport
									{
										Task = $"Converting macro with to block list: '{contentType.Name}'",
										Status = AutoBlockListConstants.Status.Success
									};

									await _client.CurrentTask(dataConvertReport.Task);

									var dataType = _dataTypeService.GetDataType(tinyMceDataType.DataTypeId);
									var editor = dataType.Editor;
									if (editor?.GetConfigurationEditor() is not RichTextConfigurationEditor configEditor)
									{
										dataConvertReport.Status = AutoBlockListConstants.Status.Failed;
										await _client.AddReport(dataConvertReport);
										continue;
									}

									richTextEditorValue = _autoBlockListMacroService.ReplaceMacroWithBlockList(macroString, parameters, configEditor, dataType, contentType, richTextEditorValue);
									hasChanges = true;
									await _client.AddReport(dataConvertReport);

									var createPartialViewReport = _autoBlockListMacroService.CreatePartialView(macro);
									await _client.AddReport(createPartialViewReport);
								}

								if (hasChanges)
									content.SetValue(tinyMceDataType.Alias, RichTextPropertyEditorHelper.SerializeRichTextEditorValue(richTextEditorValue, _jsonSerializer));
							}
							else
							{
								tinyMceDataTypeReport.Status = AutoBlockListConstants.Status.Skipped;
								await _client.AddReport(tinyMceDataTypeReport);
								continue;
							}
						}
					}

					await _client.CurrentTask("Saving");

					if (_autoBlockListService.GetSaveAndPublishSetting())
					{

						_contentService.SaveAndPublish(content);
						await _client.AddReport(new ConvertReport()
						{
							Task = $"Saving and publishing content: '{content.Name}'",
							Status = AutoBlockListConstants.Status.Success
						});
					}
					else
					{
						_contentService.Save(content);
						await _client.AddReport(new ConvertReport()
						{
							Task = $"Saving content: {content.Name}",
							Status = AutoBlockListConstants.Status.Success
						});
					}

					await _client.AddReport(contentMacroReport);
					await _client.Done(index + 1);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to convert");
				return ValidationProblem(ex.Message);
			}

			return Ok();
		}

		#endregion

		#region NestedContent
		[HttpGet]
		public PagedResult<DisplayAutoBlockListContent> GetAllContentWithNC(int page)
		{
			var contentTypes = _runtimeCache.GetCacheItem(AutoBlockListConstants.CacheKey, () =>
			{
				var contentTypes = GetAllNCContentTypes(PropertyEditors.Aliases.NestedContent);

				return contentTypes != null && contentTypes.Any() ? contentTypes : null;
			});
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

		private async Task<ConvertReport> ConvertNCDataType(int id)
		{
			var convertReport = new ConvertReport()
			{
				Task = string.Format("Converting NC data type with id {0} to Block list", id),
			};

			try
			{
				IDataType dataType = _dataTypeService.GetDataType(id);
				convertReport.Task = string.Format("Converting '{0}' to Block list", dataType.Name);

				await _client.UpdateItem(convertReport.Task);

				var blDataType = _autoBlockListService.CreateBLDataType(dataType);
				var existingDataType = _dataTypeService.GetDataType(blDataType.Name);

				if (blDataType.Name != existingDataType?.Name)
				{
					_dataTypeService.Save(blDataType);

					convertReport.Status = AutoBlockListConstants.Status.Success;

					await _client.AddReport(convertReport);
					return convertReport;
				}

				convertReport.Status = AutoBlockListConstants.Status.Skipped;

				await _client.AddReport(convertReport);

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
		public async Task<ConvertReport> AddDataTypeToContentType(IContentType contentType, IDataType ncDataType)
		{
			var blDataType = _dataTypeService.GetDataType(string.Format(_autoBlockListService.GetNameFormatting(), ncDataType.Name));
			var convertReport = new ConvertReport()
			{
				Task = string.Format("Adding data type '{0}' to document type '{1}'", blDataType.Name, contentType.Name),
				Status = AutoBlockListConstants.Status.Failed
			};

			await _client.UpdateItem(convertReport.Task);

			try
			{

				var propertyType = contentType.PropertyTypes.FirstOrDefault(x => x.DataTypeId == ncDataType.Id);
				var isComposition = contentType.CompositionIds().Any();

				propertyType = isComposition ? contentType.CompositionPropertyTypes.FirstOrDefault(x => x.DataTypeId == ncDataType.Id) : propertyType;

				if (contentType.PropertyTypeExists(string.Format(_autoBlockListService.GetAliasFormatting(), propertyType.Alias)))
				{
					convertReport.Status = AutoBlockListConstants.Status.Skipped;
					await _client.AddReport(convertReport);
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
								await _client.AddReport(convertReport);
								return convertReport;
							}

							compositionContentType.AddPropertyType(_autoBlockListService.MapPropertyType(propertyType, ncDataType, blDataType),
									compositionContentType.PropertyGroups.FirstOrDefault(x => x.Id == propertyType.PropertyGroupId.Value).Alias);
							_contentTypeService.Save(compositionContentType);
							convertReport.Status = AutoBlockListConstants.Status.Success;

						}
					}
				}

				if (contentType.PropertyTypeExists(propertyType.Alias))
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

			await _client.AddReport(convertReport);

			return convertReport;
		}

		private async Task TransferContent(int id)
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

				await _client.AddReport(convertReport);
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

						await _client.UpdateItem(report.Task);

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

						await _client.AddReport(report);
					}
				}
				else
				{
					var report = new ConvertReport()
					{
						Task = string.Format("Converting '{0}' to block list content", ncProperty.PropertyType.Name),
						Status = AutoBlockListConstants.Status.Failed
					};

					await _client.UpdateItem(report.Task);

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

					await _client.AddReport(report);
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
		public async Task<IActionResult> ConvertNC(ConvertDto dto)
		{
			try
			{
				_client = new AutoBlockListHubClient(_hubContext, dto.ConnectionId);

				foreach (var content in dto.Contents)
				{
					var index = dto.Contents.FindIndex(x => x == content);

					await _client.CurrentTask("Converting data types");
					await _client.SetTitle(content.Name);
					await _client.SetSubTitle(index);
					await _client.UpdateStep("dataTypes");

					var fullContentType = _contentTypeService.Get(content.ContentTypeKey);

					var ncDataTypes = _autoBlockListService.GetDataTypesInContentType(fullContentType).ToArray();

					foreach (var dataType in ncDataTypes)
					{
						var convertReport = await ConvertNCDataType(dataType.Id);
						if (convertReport.Status == AutoBlockListConstants.Status.Failed)
						{
							await _client.Done(index);
							return ValidationProblem();
						}
					}

					await _client.CurrentTask("Adding data type to document type");
					await _client.UpdateStep("contentTypes");

					foreach (var dataType in ncDataTypes)
					{
						var hasNcDataType = fullContentType.PropertyTypes.FirstOrDefault(x => x.DataTypeId == dataType.Id);
						hasNcDataType = hasNcDataType == null ? fullContentType.CompositionPropertyTypes.FirstOrDefault(x => x.DataTypeId == dataType.Id) : hasNcDataType;

						if (hasNcDataType != null)
						{
							var convertReport = await AddDataTypeToContentType(fullContentType, dataType);
							if (convertReport.Status == AutoBlockListConstants.Status.Failed)
							{
								await _client.Done(index);
								return ValidationProblem();
							}
						}
						else
						{
							var contentTypes = _autoBlockListService.GetElementContentTypesFromDataType(dataType);
							foreach (var contentType in contentTypes)
							{
								var convertReport = await AddDataTypeToContentType(contentType, dataType);
								if (convertReport.Status == AutoBlockListConstants.Status.Failed)
								{
									await _client.Done(index);
									return ValidationProblem();
								}
							}
						}
					}

					await _client.CurrentTask("Converting content");
					await _client.UpdateStep("content");
					await TransferContent(content.Id);

					await _client.Done(index + 1);
				}

			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to convert");
				return ValidationProblem(ex.Message);
			}

			return Ok();
		}
		#endregion
	}
}