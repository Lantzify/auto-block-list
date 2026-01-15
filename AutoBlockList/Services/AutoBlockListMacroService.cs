using System.Text;
using Newtonsoft.Json;
using Umbraco.Cms.Core;
using AutoBlockList.Dtos;
using Umbraco.Extensions;
using Umbraco.Cms.Core.IO;
using Newtonsoft.Json.Linq;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Services;
using CSharpTest.Net.Collections;
using AutoBlockList.Dtos.BlockList;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Serialization;
using Microsoft.Extensions.Primitives;
using Umbraco.Cms.Core.PropertyEditors;
using AutoBlockList.Services.interfaces;
using static Umbraco.Cms.Core.Constants;
using static Umbraco.Cms.Core.PropertyEditors.RichTextConfiguration;

namespace AutoBlockList.Services
{
	public class AutoBlockListMacroService : IAutoBlockListMacroService
	{
		private readonly ILogger<AutoBlockListMacroService> _logger;
		private readonly FileSystems _fileSystem;
		private readonly IFileService _fileService;
		private readonly IMacroService _macroService;
		private readonly IJsonSerializer _jsonSerializer;
		private readonly IAutoBlockListContext _hubContext;
		private readonly IDataTypeService _dataTypeService;
		private readonly IShortStringHelper _shortStringHelper;
		private readonly IContentTypeService _contentTypeService;
		private readonly IOptions<AutoBlockListSettings> _autoBlockListSettings;

		private static readonly string _partialViewDirectory = "/Views/Partials/blocklist/Components/";

		private static readonly Regex MacroRegex = new Regex(
			@"(?is)(<\?UMBRACO_MACRO\b[^>]*?/>)|(<!--\?umb_macro\b.*?-->)|(\bumb-macro-holder\b)|(\bdata-macro-alias\b)",
			RegexOptions.Compiled);

		private static readonly Regex MacroParametersRegex = new Regex(
			@"(?is)\b(?<param>\w+)\s*=\s*(?:\\)?['""](?<value>[^'""]*)['""]",
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);


		public AutoBlockListMacroService(ILogger<AutoBlockListMacroService> logger,
			FileSystems fileSystem,
			IFileService fileService,
			IMacroService macroService,
			IJsonSerializer jsonSerializer,
			IAutoBlockListContext hubContext,
			IDataTypeService dataTypeService,
			IShortStringHelper shortStringHelper,
			IContentTypeService contentTypeService,
			IOptions<AutoBlockListSettings> autoBlockListSettings)
		{
			_logger = logger;
			_fileSystem = fileSystem;
			_fileService = fileService;
			_jsonSerializer = jsonSerializer;
			_macroService = macroService;
			_hubContext = hubContext;
			_dataTypeService = dataTypeService;
			_shortStringHelper = shortStringHelper;
			_contentTypeService = contentTypeService;
			_autoBlockListSettings = autoBlockListSettings;
		}

		public string GetFolderNameForContentTypes() => _autoBlockListSettings.Value.FolderNameForContentTypes;

		public bool HasMacro(string content) => !string.IsNullOrEmpty(content) && MacroRegex.IsMatch(content);



		public string[] GetMacroStrings(string content)
		{
			return MacroRegex
					.Matches(content)
					.Cast<Match>()
					.Select(x => x.Value)
					.Where(a => !string.IsNullOrWhiteSpace(a))
					.ToArray();
		}

		public Dictionary<string, object> GetParametersFromMaco(string macroString)
		{
			var parameters = new Dictionary<string, object>();

			if (string.IsNullOrEmpty(macroString))
				return parameters;

			var matches = MacroParametersRegex.Matches(macroString);

			foreach (Match match in matches)
			{
				if (match.Success && match.Groups["param"].Success && match.Groups["value"].Success)
				{
					string paramName = match.Groups["param"].Value;
					string paramValue = match.Groups["value"].Value;

					parameters[paramName] = paramValue;
				}
			}

			return parameters;
		}

		public string ProcessTinyMceContentForMacroConversion(string tinyMceContent, IPropertyType tinyMceDataType, string? culture = null)
		{
			var macroStringReport = new ConvertReport
			{
				Status = AutoBlockListConstants.Status.Success
			};

			if (string.IsNullOrEmpty(tinyMceContent) || !HasMacro(tinyMceContent))
				return string.Empty;

			try
			{
				RichTextPropertyEditorHelper.TryParseRichTextEditorValue(tinyMceContent, _jsonSerializer, _logger, out var richTextEditorValue);
				var macros = GetMacroStrings(richTextEditorValue.Markup);

				foreach (var macroString in macros)
				{
					macroStringReport.Task = $"Converting macro: '{macroString}' to document type {(!string.IsNullOrEmpty(culture) ? $"with culture:{culture}" : "")}";

					_hubContext.Client?.CurrentTask(macroStringReport.Task);

					var parameters = GetParametersFromMaco(macroString);
					if (string.IsNullOrEmpty(parameters["macroAlias"].ToString()))
					{
						macroStringReport.ErrorMessage = "Macro alias parameter is missing.";
						_hubContext.Client?.AddReport(macroStringReport);
						continue;
					}

					var macro = _macroService.GetByAlias(parameters["macroAlias"]?.ToString() ?? "");
					if (macro == null)
					{
						macroStringReport.ErrorMessage = $"Macro with alias '{parameters["macroAlias"]}' not found.";
						_hubContext.Client?.AddReport(macroStringReport);
						continue;
					}

					var contentType = ConvertMacroToContentType(macro);
					if (contentType == null)
					{
						macroStringReport.ErrorMessage = $"Failed to convert macro '{macro.Name}' to content type.";
						_hubContext.Client?.AddReport(macroStringReport);
						continue;
					}

					macroStringReport.Status = AutoBlockListConstants.Status.Success;
					_hubContext.Client?.AddReport(macroStringReport);



					var dataConvertReport = new ConvertReport
					{
						Task = $"Converting macro to block list: '{contentType.Name}' for culture: '{culture}'",
						Status = AutoBlockListConstants.Status.Success
					};

					_hubContext.Client?.CurrentTask(dataConvertReport.Task);

					var dataType = _dataTypeService.GetDataType(tinyMceDataType.DataTypeId);
					if (dataType?.Editor?.GetConfigurationEditor() is not RichTextConfigurationEditor configEditor)
					{
						dataConvertReport.Status = AutoBlockListConstants.Status.Failed;
						_hubContext.Client?.AddReport(dataConvertReport);
						continue;
					}

					_hubContext.Client?.AddReport(dataConvertReport);

					richTextEditorValue = ReplaceMacroWithBlockList(macroString, parameters, configEditor, dataType, contentType, richTextEditorValue);

					var createPartialViewReport = CreatePartialView(macro);
					_hubContext.Client?.AddReport(createPartialViewReport);
				}

				return RichTextPropertyEditorHelper.SerializeRichTextEditorValue(richTextEditorValue, _jsonSerializer);
			}
			catch (Exception ex)
			{
				macroStringReport.Status = AutoBlockListConstants.Status.Failed;
				macroStringReport.ErrorMessage = ex.Message;
				_hubContext.Client?.AddReport(macroStringReport);
				_logger.LogError(ex, "Error processing content for macro conversion: {Message}", ex.Message);
				return string.Empty;
			}
		}

		public string ProcessBlockListValues(string stringValue, IEnumerable<Guid> contentTypeKeys, string culture = null)
		{
			var blockList = JsonConvert.DeserializeObject<BlockList>(stringValue);
			if (blockList == null)
				return string.Empty;

			var contentBlocksWithRte = blockList.contentData.Where(x => contentTypeKeys.Contains(Guid.Parse(x.GetValue("contentTypeKey"))));
			foreach (var contentBlock in contentBlocksWithRte)
			{
				if (!contentBlock.TryGetValue("contentTypeKey", out string contentTypeKeyString))
				{
					continue;
				}

				Guid contentTypeKey = Guid.Parse(contentTypeKeyString);
				var contentTypeWithRichTextEditor = _contentTypeService.Get(contentTypeKey);

				if (contentTypeWithRichTextEditor == null)
					continue;

				var tinyMceProperies = contentTypeWithRichTextEditor.PropertyTypes.ToList();
				tinyMceProperies.AddRange(contentTypeWithRichTextEditor.CompositionPropertyTypes);

				var aliases = tinyMceProperies.Select(x => x.Alias);

				var rawPropertyValues = contentBlock.Where(x => aliases.Contains(x.Key));
				foreach (var rawPropertyValue in rawPropertyValues)
				{
					var tinyMceProperty = tinyMceProperies.FirstOrDefault(x => x.Alias == rawPropertyValue.Key);

					if (tinyMceProperty == null)
						continue;

					if (tinyMceProperty.PropertyEditorAlias == PropertyEditors.Aliases.BlockList)
					{
						var nestedBlocklist = ProcessBlockListValues(rawPropertyValue.Value, contentTypeKeys, culture);
						if (!string.IsNullOrEmpty(nestedBlocklist) && nestedBlocklist != contentBlock[rawPropertyValue.Key])
							contentBlock[rawPropertyValue.Key] = nestedBlocklist;
					}
					else if (tinyMceProperty.PropertyEditorAlias == PropertyEditors.Aliases.TinyMce)
					{
						var updatedValue = ProcessTinyMceContentForMacroConversion(rawPropertyValue.Value, tinyMceProperty, culture);

						if (string.IsNullOrEmpty(updatedValue))
							continue;

						contentBlock[rawPropertyValue.Key] = updatedValue;
					}
				}
			}

			return JsonConvert.SerializeObject(blockList);
		}

		public bool ProcessContentForMacroConversion(IContent content, IPropertyType tinyMceDataType, string culture = null)
		{
			var tinyMceContent = content.GetValue<string>(tinyMceDataType.Alias, culture);

			var macroStringReport = new ConvertReport
			{
				Status = AutoBlockListConstants.Status.Success
			};

			bool hasChanges = false;

			if (string.IsNullOrEmpty(tinyMceContent) || !HasMacro(tinyMceContent))
				return hasChanges;

			try
			{
				var newRichTextEditorValue = ProcessTinyMceContentForMacroConversion(tinyMceContent, tinyMceDataType, culture);

				if (!string.IsNullOrEmpty(newRichTextEditorValue)) 
				{
					hasChanges = true;
					content.SetValue(tinyMceDataType.Alias, newRichTextEditorValue, culture);
				}
					
				return hasChanges;
			}
			catch (Exception ex)
			{
				macroStringReport.Status = AutoBlockListConstants.Status.Failed;
				macroStringReport.ErrorMessage = ex.Message;
				_hubContext.Client?.AddReport(macroStringReport);
				_logger.LogError(ex, "Error processing content for macro conversion: {Message}", ex.Message);
				return false;
			}
		}

		public ContentType ConvertMacroToContentType(IMacro macro)
		{
			string folderName = GetFolderNameForContentTypes();
			_contentTypeService.GetContainer(-1);
			var existingContainer = _contentTypeService.GetContainer(AutoBlockListConstants.ContentTypeFolderGuid);
			int existingId = existingContainer?.Id ?? -1;

			if (existingContainer == null)
			{
				var attempt = _contentTypeService.CreateContainer(-1, AutoBlockListConstants.ContentTypeFolderGuid, folderName);
				if (attempt.Success)
					existingId = attempt.Result.Entity.Id;
			}

			var contentTypeReport = new ConvertReport
			{
				Task = $"Creating document type for macro '{macro.Name}'",
				Status = AutoBlockListConstants.Status.Failed
			};

			try
			{
				var alreadyExists = TryGetContentTypeByAlias(macro.Alias, out string newAlias);
				if (alreadyExists != null)
				{
					contentTypeReport.Status = AutoBlockListConstants.Status.Skipped;
					_hubContext.Client?.AddReport(contentTypeReport);

					return alreadyExists;
				}

				ContentType contentType = new ContentType(_shortStringHelper, existingId)
				{
					Alias = !string.IsNullOrEmpty(newAlias) ? newAlias : macro.Alias,
					Name = macro.Name,
					Icon = "icon-settings-alt",
					IsElement = true,

				};

				var parametersGroup = new PropertyGroup(new PropertyTypeCollection(contentType.SupportsPublishing))
				{
					Alias = "parameters",
					Name = "Parameters",
					Type = PropertyGroupType.Tab,
					SortOrder = 0,
				};

				contentType.PropertyGroups.Add(parametersGroup);


				foreach (var property in macro.Properties)
				{
					var dataTypes = _dataTypeService.GetByEditorAlias(property.EditorAlias);

					if (dataTypes == null || !dataTypes.Any())
					{
						_hubContext.Client?.AddReport(new ConvertReport()
						{
							Task = $"Skipping property '{property.Name}' for macro '{macro.Name}' - No matching data type found for editor alias '{property.EditorAlias}'",
							Status = AutoBlockListConstants.Status.Skipped
						});

						continue;
					}

					parametersGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, dataTypes.FirstOrDefault())
					{
						Alias = property.Alias,
						Name = property.Name,

					});
				}

				_contentTypeService.Save(contentType);

				contentTypeReport.Status = AutoBlockListConstants.Status.Success;
				_hubContext.Client?.AddReport(contentTypeReport);

				return contentType;
			}
			catch (Exception ex)
			{
				_hubContext.Client?.AddReport(contentTypeReport);
				_logger.LogError(ex, "Error converting macro to content type: {Message}", ex.Message);
				return null;
			}
		}

		private ContentType TryGetContentTypeByAlias(string alias, out string newAlias)
		{
			var firstAttempt = _contentTypeService.Get(alias);
			if (firstAttempt != null)
			{
				var hasParametersGroup = firstAttempt.PropertyGroups.Any(x => x.Alias == "parameters");
				if (hasParametersGroup)
				{
					newAlias = firstAttempt.Alias;
					return firstAttempt as ContentType;
				}
				else
				{
					newAlias = "macro" + alias;
					return TryGetContentTypeByAlias(newAlias, out string temp);
				}
			}
			else
			{
				newAlias = alias;
				return null;
			}
		}

		public RichTextEditorValue ReplaceMacroWithBlockList(string macroString, Dictionary<string, object> parameters, RichTextConfigurationEditor configEditor, IDataType dataType, ContentType contentType, RichTextEditorValue richTextEditorValue)
		{
			var currentConfig = dataType.Configuration as RichTextConfiguration;
			var blocks = currentConfig?.Blocks?.ToList() ?? new List<RichTextBlockConfiguration>();

			if (!blocks.Any(b => b.ContentElementTypeKey == contentType.Key))
			{
				blocks.Add(new RichTextBlockConfiguration
				{
					ContentElementTypeKey = contentType.Key,
					Label = contentType.Name,
					EditorSize = "small"
				});


				var configValues = configEditor.ToConfigurationEditor(currentConfig);
				configValues["blocks"] = blocks;
				dataType.Configuration = configEditor.FromConfigurationEditor(configValues, currentConfig);
				_dataTypeService.Save(dataType);
			}

			var contentUdi = new GuidUdi("element", Guid.NewGuid());

			richTextEditorValue.Blocks ??= new BlockValue();
			richTextEditorValue.Blocks.ContentData.Add(new BlockItemData()
			{
				Udi = contentUdi,
				ContentTypeKey = contentType.Key,
				RawPropertyValues = parameters.Where(x => x.Key != "macroAlias").ToDictionary()
			});

			var layoutItem = new RichTextBlockLayoutItem()
			{
				ContentUdi = contentUdi,
			};


			richTextEditorValue.Blocks.Layout ??= new Dictionary<string, JToken>();

			var currentLayout = new List<RichTextBlockLayoutItem>();
			if (richTextEditorValue.Blocks.Layout.ContainsKey(PropertyEditors.Aliases.TinyMce))
			{
				var existingLayoutToken = richTextEditorValue.Blocks.Layout[PropertyEditors.Aliases.TinyMce];
				if (existingLayoutToken != null)
				{
					currentLayout = existingLayoutToken.ToObject<List<RichTextBlockLayoutItem>>() ?? new List<RichTextBlockLayoutItem>();
				}
			}

			currentLayout.Add(layoutItem);

			richTextEditorValue.Blocks.Layout[PropertyEditors.Aliases.TinyMce] = JToken.FromObject(currentLayout);
			richTextEditorValue.Markup = richTextEditorValue.Markup.ReplaceFirst(macroString, $"<umb-rte-block data-content-udi=\"{contentUdi}\"></umb-rte-block>");
			return richTextEditorValue;
		}

		public ConvertReport CreatePartialView(IMacro macro)
		{
			ConvertReport report = new ConvertReport
			{
				Task = $"Creating partial view for macro '{macro.Name}'",
				Status = AutoBlockListConstants.Status.Skipped
			};

			string macroPartialView = macro.Name + ".cshtml";
			var partialViews = _fileSystem.PartialViewsFileSystem.GetFiles(_partialViewDirectory);

			if (!partialViews.Any(x => x.Contains(macroPartialView)))
			{
				var partialviewMacro = _fileService.GetPartialViewMacro(macroPartialView);

				if (partialviewMacro != null)
				{
					string content = partialviewMacro.Content?.Replace("@inherits Umbraco.Cms.Web.Common.Macros.PartialViewMacroPage", "@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<Umbraco.Cms.Core.Models.Blocks.BlockListModel>") ?? "";

					using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
					_fileSystem.PartialViewsFileSystem.AddFile(_partialViewDirectory + macroPartialView, stream);

					report.Status = AutoBlockListConstants.Status.Success;
				}
			}

			return report;
		}
	}
}
