using System.Text;
using Umbraco.Cms.Core;
using AutoBlockList.Dtos;
using Umbraco.Extensions;
using Umbraco.Cms.Core.IO;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Services;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.PropertyEditors;
using static Umbraco.Cms.Core.Constants;
using AutoBlockList.Services.interfaces;
using static Umbraco.Cms.Core.PropertyEditors.RichTextConfiguration;

namespace AutoBlockList.Services
{
	public class AutoBlockListMacroService : IAutoBlockListMacroService
	{
		private readonly FileSystems _fileSystem;
		private readonly IFileService _fileService;
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


		public AutoBlockListMacroService(FileSystems fileSystem,
			IFileService fileService,
			IDataTypeService dataTypeService,
			IShortStringHelper shortStringHelper,
			IContentTypeService contentTypeService,
			IOptions<AutoBlockListSettings> autoBlockListSettings)
		{
			_fileSystem = fileSystem;
			_fileService = fileService;
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


		public ContentType ConvertMacroToContentType(IMacro macro, out List<ConvertReport> reports)
		{
			reports = new List<ConvertReport>();

			string folderName = GetFolderNameForContentTypes();
			var containers = _contentTypeService.GetContainers(folderName, -1);
			var existingContainer = containers?.FirstOrDefault();
			int existingId = existingContainer?.Id ?? -1;

			if (existingContainer == null)
			{
				var existingFolders = new ConvertReport
				{
					Task = $"Creating folder '{folderName}'",
					Status = Constants.AutoBlockListConstants.Status.Skipped
				};

				var attempt = _contentTypeService.CreateContainer(-1, Guid.NewGuid(), folderName);
				if (attempt.Success)
				{
					existingId = attempt.Result.Entity.Id;
					existingFolders.Status = Constants.AutoBlockListConstants.Status.Success;
				}

				reports.Add(existingFolders);
			}

			var contentTypeReport = new ConvertReport
			{
				Task = $"Creating document type for macro '{macro.Name}'",
				Status = Constants.AutoBlockListConstants.Status.Skipped
			};

			var alreadyExists = _contentTypeService.Get(macro.Alias);
			if (alreadyExists != null)
			{
				reports.Add(contentTypeReport);
				return alreadyExists as ContentType;
			}
				
			ContentType contentType = new ContentType(_shortStringHelper, existingId)
			{
				Alias = macro.Alias,
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
					continue;


				parametersGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, dataTypes.FirstOrDefault())
				{
					Alias = property.Alias,
					Name = property.Name,
					
				});
			}
		
			_contentTypeService.Save(contentType);

			reports.Add(contentTypeReport);

			return contentType;
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
				Status = Constants.AutoBlockListConstants.Status.Skipped
			};

			string macroPartialView = macro.Alias + ".cshtml";
			var partialViews = _fileSystem.PartialViewsFileSystem.GetFiles(_partialViewDirectory);

			if (!partialViews.Any(x => x.Contains(macroPartialView)))
			{
				var partialviewMacro = _fileService.GetPartialViewMacro(macroPartialView);

				if (partialviewMacro != null)
				{
					string content = partialviewMacro.Content?.Replace("@inherits Umbraco.Cms.Web.Common.Macros.PartialViewMacroPage", "@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<Umbraco.Cms.Core.Models.Blocks.BlockListModel>") ?? "";

					using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
					_fileSystem.PartialViewsFileSystem.AddFile(_partialViewDirectory + macroPartialView, stream);

					report.Status = Constants.AutoBlockListConstants.Status.Success;
				}
			}

			return report;
		}
	}
}
