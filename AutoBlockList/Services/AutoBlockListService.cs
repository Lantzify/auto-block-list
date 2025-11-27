using Newtonsoft.Json;
using Umbraco.Cms.Core;
using AutoBlockList.Dtos;
using Umbraco.Extensions;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Services;
using AutoBlockList.Dtos.BlockList;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.PropertyEditors;
using AutoBlockList.Services.interfaces;
using static Umbraco.Cms.Core.Constants;
using DataType = Umbraco.Cms.Core.Models.DataType;
using static Umbraco.Cms.Core.PropertyEditors.BlockListConfiguration;

namespace AutoBlockList.Services
{
    public class AutoBlockListService : IAutoBlockListService
    {
		private readonly ILogger<AutoBlockListService> _logger;
        private readonly IContentService _contentService;
		private readonly IAutoBlockListContext _hubContext;
		private readonly IDataTypeService _dataTypeService;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataValueEditorFactory _dataValueEditorFactory;
        private readonly PropertyEditorCollection _propertyEditorCollection;
        private readonly IOptions<AutoBlockListSettings> _dataBlockConverterSettings;
        private readonly IConfigurationEditorJsonSerializer _configurationEditorJsonSerializer;

        public AutoBlockListService(ILogger<AutoBlockListService> logger,
			IContentService contentService,
			IAutoBlockListContext hubContext,
            IDataTypeService dataTypeService,
            IShortStringHelper shortStringHelper,
            IContentTypeService contentTypeService,
            IDataValueEditorFactory dataValueEditorFactory,
            PropertyEditorCollection propertyEditorCollection,
            IOptions<AutoBlockListSettings> dataBlockConverterSettings,
            IConfigurationEditorJsonSerializer configurationEditorJsonSerializer)
        {
            _logger = logger;
            _contentService = contentService;
			_hubContext = hubContext;
			_dataTypeService = dataTypeService;
            _shortStringHelper = shortStringHelper;
            _contentTypeService = contentTypeService;
            _dataValueEditorFactory = dataValueEditorFactory;
            _propertyEditorCollection = propertyEditorCollection;
            _dataBlockConverterSettings = dataBlockConverterSettings;
            _configurationEditorJsonSerializer = configurationEditorJsonSerializer;
        }

        public string GetNameFormatting() => _dataBlockConverterSettings.Value.NameFormatting;
        public string GetAliasFormatting() => _dataBlockConverterSettings.Value.AliasFormatting;
        public bool GetSaveAndPublishSetting() => _dataBlockConverterSettings.Value.SaveAndPublish;
        public string GetBlockListEditorSize() => _dataBlockConverterSettings.Value.BlockListEditorSize;

        public IDataType? CreateBLDataType(IDataType ncDataType)
        {
            var ncConfig = ncDataType.Configuration as NestedContentConfiguration;

            var blDataType = new DataType(new DataEditor(_dataValueEditorFactory), _configurationEditorJsonSerializer)
            {
                Editor = _propertyEditorCollection.First(x => x.Alias == PropertyEditors.Aliases.BlockList),
                CreateDate = DateTime.Now,
                Name = string.Format(GetNameFormatting(), ncDataType.Name),
                Configuration = new BlockListConfiguration()
                {
                    ValidationLimit = new NumberRange()
                    {
                        Max = ncConfig?.MaxItems,
                        Min = ncConfig?.MinItems
                    },
                },
            };

            var blConfig = blDataType.Configuration as BlockListConfiguration;
            var blocks = new List<BlockConfiguration>();

            foreach (var ncContentType in ncConfig.ContentTypes)
            {
                blocks.Add(new BlockConfiguration()
                {
                    Label = ncContentType.Template,
                    EditorSize = GetBlockListEditorSize(),
                    ContentElementTypeKey = _contentTypeService.Get(ncContentType.Alias).Key
                });
            }

            blConfig.Blocks = blocks.ToArray();

            return blDataType;
        }

		public ConvertReport ConvertNCDataType(int id)
		{
			var convertReport = new ConvertReport()
			{
				Task = string.Format("Converting NC data type with id {0} to Block list", id),
			};

			try
			{
				IDataType dataType = _dataTypeService.GetDataType(id);
				convertReport.Task = string.Format("Converting '{0}' to Block list", dataType.Name);

                _hubContext.Client?.UpdateItem(convertReport.Task);

				var blDataType = CreateBLDataType(dataType);
				var existingDataType = _dataTypeService.GetDataType(blDataType.Name);

				if (blDataType.Name != existingDataType?.Name)
				{
					_dataTypeService.Save(blDataType);

					convertReport.Status = AutoBlockListConstants.Status.Success;

					_hubContext.Client?.AddReport(convertReport);
					return convertReport;
				}

				convertReport.Status = AutoBlockListConstants.Status.Skipped;

				_hubContext.Client?.AddReport(convertReport);

				return convertReport;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, string.Format("Failed to convert NC with id '{0}' to block list.", id));

				convertReport.ErrorMessage = AutoBlockListConstants.CheckLogs;
				convertReport.Status = AutoBlockListConstants.Status.Failed;

				return convertReport;
			}
		}

		public ConvertReport AddDataTypeToContentType(IContentType contentType, IDataType ncDataType)
		{
			var blDataType = _dataTypeService.GetDataType(string.Format(GetNameFormatting(), ncDataType.Name));
			var convertReport = new ConvertReport()
			{
				Task = string.Format("Adding data type '{0}' to document type '{1}'", blDataType.Name, contentType.Name),
				Status = AutoBlockListConstants.Status.Failed
			};

			_hubContext.Client?.UpdateItem(convertReport.Task);

			try
			{

				var propertyType = contentType.PropertyTypes.FirstOrDefault(x => x.DataTypeId == ncDataType.Id);
				var isComposition = contentType.CompositionIds().Any();

				propertyType = isComposition ? contentType.CompositionPropertyTypes.FirstOrDefault(x => x.DataTypeId == ncDataType.Id) : propertyType;

				if (contentType.PropertyTypeExists(string.Format(GetAliasFormatting(), propertyType.Alias)))
				{
					convertReport.Status = AutoBlockListConstants.Status.Skipped;
					_hubContext.Client?.AddReport(convertReport);
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
							if (compositionContentType.PropertyTypeExists(string.Format(GetAliasFormatting(), propertyType.Alias)))
							{
								convertReport.Status = AutoBlockListConstants.Status.Skipped;
								_hubContext.Client?.AddReport(convertReport);
								return convertReport;
							}

							compositionContentType.AddPropertyType(MapPropertyType(propertyType, ncDataType, blDataType),
									compositionContentType.PropertyGroups.FirstOrDefault(x => x.Id == propertyType.PropertyGroupId.Value).Alias);
							_contentTypeService.Save(compositionContentType);
							convertReport.Status = AutoBlockListConstants.Status.Success;

						}
					}
				}

				if (contentType.PropertyTypeExists(propertyType.Alias))
				{
					contentType.AddPropertyType(MapPropertyType(propertyType, ncDataType, blDataType),
												contentType.PropertyGroups.FirstOrDefault(x => x.Id == propertyType.PropertyGroupId.Value).Alias);
					_contentTypeService.Save(contentType);
					convertReport.Status = AutoBlockListConstants.Status.Success;
				}

			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to add block list to document type");
				_hubContext.Client?.Done("failed");
				convertReport.ErrorMessage = AutoBlockListConstants.CheckLogs;
			}

			_hubContext.Client?.AddReport(convertReport);

			return convertReport;
		}

		public IEnumerable<CustomDisplayDataType> GetAllDataTypesWithAlias(string alias)
        {
            var dataTypes = new List<CustomDisplayDataType>();
            foreach (var dataType in _dataTypeService.GetAll().Where(x => x.EditorAlias == alias))
                dataTypes.Add(new CustomDisplayDataType()
                {
                    Id = dataType.Id,
                    Name = dataType.Name,
                    Icon = dataType.Editor?.Icon
                });

            return dataTypes;
        }

		public void TransferContent(int id)
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

				_hubContext.Client?.AddReport(convertReport);
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

						_hubContext.Client?.UpdateItem(report.Task);

						try
						{
							var value = ConvertPropertyValueToBlockList(ncProperty, culture);
							if (!string.IsNullOrEmpty(value))
							{
								node.SetValue(string.Format(GetAliasFormatting(), ncProperty.Alias), value, culture);
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
							report.ErrorMessage = AutoBlockListConstants.CheckLogs;
						}

						_hubContext.Client?.AddReport(report);
					}
				}
				else
				{
					var report = new ConvertReport()
					{
						Task = string.Format("Converting '{0}' to block list content", ncProperty.PropertyType.Name),
						Status = AutoBlockListConstants.Status.Failed
					};

					_hubContext.Client?.UpdateItem(report.Task);

					try
					{
						var value = ConvertPropertyValueToBlockList(ncProperty);
						if (!string.IsNullOrEmpty(value))
						{
							node.SetValue(string.Format(GetAliasFormatting(), ncProperty.Alias), value);
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
						report.ErrorMessage = AutoBlockListConstants.CheckLogs;
					}

					_hubContext.Client?.AddReport(report);
				}
			}

			if (GetSaveAndPublishSetting())
			{
				_contentService.SaveAndPublish(node);
			}
			else
			{
				_contentService.Save(node);
			}
		}


		public string ConvertPropertyValueToBlockList(IProperty property, string? culture = null)
        {
            var value = property.GetValue(culture);
            if (value == null)
                return string.Empty;

            var ncValues = JsonConvert.DeserializeObject<IEnumerable<Dictionary<string, string>>>(value.ToString());

            var contentData = ConvertNCDataToBLData(ncValues);
            var contentUdiList = new List<Dictionary<string, string>>();

            if (contentData == null)
                return string.Empty;

            foreach (var content in contentData)
            {
                contentUdiList.Add(new Dictionary<string, string>
                {
                    {"contentUdi",content["udi"] },
                });
            }

            var blockList = new BlockList()
            {
                layout = new BlockListUdi(contentUdiList, new List<Dictionary<string, string>>()),
                contentData = contentData,
                settingsData = new List<Dictionary<string, string>>()
            };

            return JsonConvert.SerializeObject(blockList);
        }

        private List<Dictionary<string, string>> ConvertNCDataToBLData(IEnumerable<Dictionary<string, string>> ncValues)
        {
            if (ncValues == null)
                return null;

            var contentData = new List<Dictionary<string, string>>();

            foreach (var ncValue in ncValues)
            {
                var rawContentType = ncValue.FirstOrDefault(x => x.Key == "ncContentTypeAlias").Value;
                
                var contentType = _contentTypeService.GetAllElementTypes().FirstOrDefault(x => x.Alias == rawContentType);
                var contentUdi = new GuidUdi("element", Guid.NewGuid()).ToString();
                var values = ncValue.Where(x => !AutoBlockListConstants.DefaultNC.Contains(x.Key));

                var content = new Dictionary<string, string>
                {
                    {"contentTypeKey", contentType.Key.ToString() },
                    {"udi", contentUdi },
                };

                foreach (var value in values)
                {
                    try
                    {
                        var nsedtedNCValues = JsonConvert.DeserializeObject<IEnumerable<Dictionary<string, string>>>(value.Value);

                        if (nsedtedNCValues != null)
                        {
                            var nestedContentData = ConvertNCDataToBLData(nsedtedNCValues);
                            var contentUdiList = new List<Dictionary<string, string>>();

                            if (nestedContentData == null)
                                return null;
                     
                                foreach (var nestedContent in nestedContentData)
                                {
                                    contentUdiList.Add(new Dictionary<string, string>
                                {
                                    {"contentUdi", nestedContent["udi"] },
                                });
                                }
                                var blockList = new BlockList()
                                {
                                    layout = new BlockListUdi(contentUdiList, new List<Dictionary<string, string>>()),
                                    contentData = nestedContentData,
                                    settingsData = new List<Dictionary<string, string>>()
                                };

                                content.Add(string.Format(GetAliasFormatting(), value.Key), JsonConvert.SerializeObject(blockList));
                        }
                    }
                    catch(Exception ex)
                    {
                        content.Add(value.Key, value.Value);
                    }
                }

                contentData.Add(content);
            }

            return contentData;
        }


        public PropertyType? MapPropertyType(IPropertyType propertyType, IDataType ncDataType, IDataType blDataType)
        {
            return new PropertyType(_shortStringHelper, ncDataType)
            {
                DataTypeId = blDataType.Id,
                DataTypeKey = blDataType.Key,
                PropertyEditorAlias = blDataType.EditorAlias,
                ValueStorageType = ncDataType.DatabaseType,
                Name = propertyType.Name,
                Alias = string.Format(GetAliasFormatting(), propertyType.Alias),
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
            };
		}

        public IEnumerable<IDataType> GetDataTypesInContentType(IContentType contentType)
        {
            var dataTypes = new List<IDataType>();
            var propertyTypes = GetPropertyTypes(contentType);


            foreach (var propertyType in propertyTypes)
            {
                var dataType = _dataTypeService.GetDataType(propertyType.DataTypeId);

                if (dataType != null)
                {
                    dataTypes.Add(dataType);
                    var ncConfig = dataType.Configuration as NestedContentConfiguration;
                    foreach (var ncContentType in ncConfig.ContentTypes)
                    {
                        var nestedContentType = _contentTypeService.Get(ncContentType.Alias);
                        if (nestedContentType != null)
                            dataTypes.AddRange(GetDataTypesInContentType(nestedContentType));   
                    }
                }
            }

            return dataTypes;
        }

		public IEnumerable<int> GetComposedOf(IEnumerable<int> ids)
		{
			var contentTypes = new List<int>();

			foreach (int id in ids)
				contentTypes.AddRange(_contentTypeService.GetComposedOf(id).Select(x => x.Id));

			return contentTypes;
		}

		public IEnumerable<IPropertyType> GetPropertyTypes(IContentType contentType)
        {
            var propertyTypes = new List<IPropertyType>();
            propertyTypes.AddRange(contentType.PropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent));

            if (contentType.CompositionPropertyTypes.Any())
                propertyTypes.AddRange(contentType.CompositionPropertyTypes.Where(x => x.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent));

            return propertyTypes;
        }

        public IEnumerable<IContentType> GetElementContentTypesFromDataType(IDataType dataType)
        {
            var contentTypes = new List<IContentType>();

            var usages = _dataTypeService.GetReferences(dataType.Id);

            foreach (var entityType in usages.Where(x => x.Key.EntityType == UmbracoObjectTypes.DocumentType.GetUdiType()))
            {
                var contentType = _contentTypeService.Get(((GuidUdi)entityType.Key).Guid);

                if (contentType != null && contentType.IsElement)
                    contentTypes.Add(contentType);
            }

            return contentTypes;
        }

        public bool HasBLContent(IContent item)
        {
            var ok = new List<bool>();

            var ncProperties = item.Properties.Where(x => x.PropertyType.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent);

            foreach (var property in ncProperties)
            {
                var blProperty = item.Properties.FirstOrDefault(x => x.Alias == string.Format(GetAliasFormatting(), property.Alias));
                if (blProperty != null)
                {
                    if (property.PropertyType.VariesByCulture())
                    {
                        foreach (var language in item.AvailableCultures)
                        {
                            var ncValue = property.GetValue(language);
                            var blValue = blProperty.GetValue(language);

                            ok.Add(ncValue != null && blValue != null || ncValue == null && blValue == null);
                        }
                    }
                    else
                    {
                        var ncValue = property.GetValue();
                        var blValue = blProperty.GetValue();

                        ok.Add(ncValue != null && blValue != null || ncValue == null && blValue == null);
                    }
                }
                else
                {
                    ok.Add(false);
                }
            }

            return ok.All(x => x);
        }
    }
}