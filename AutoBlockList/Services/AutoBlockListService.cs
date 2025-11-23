using Newtonsoft.Json;
using Umbraco.Cms.Core;
using Umbraco.Extensions;
using AutoBlockList.Dtos;
using Umbraco.Cms.Core.Models;
using AutoBlockList.Constants;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Services;
using Microsoft.Extensions.Options;
using AutoBlockList.Dtos.BlockList;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.PropertyEditors;
using static Umbraco.Cms.Core.Constants;
using AutoBlockList.Services.interfaces;
using DataType = Umbraco.Cms.Core.Models.DataType;
using static Umbraco.Cms.Core.PropertyEditors.BlockListConfiguration;

namespace AutoBlockList.Services
{
    public class AutoBlockListService : IAutoBlockListService
    {
        private readonly IDataTypeService _dataTypeService;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataValueEditorFactory _dataValueEditorFactory;
        private readonly PropertyEditorCollection _propertyEditorCollection;
        private readonly IOptions<AutoBlockListSettings> _dataBlockConverterSettings;
        private readonly IConfigurationEditorJsonSerializer _configurationEditorJsonSerializer;

        public AutoBlockListService(IDataTypeService dataTypeService,
            IShortStringHelper shortStringHelper,
            IContentTypeService contentTypeService,
            IDataValueEditorFactory dataValueEditorFactory,
            PropertyEditorCollection propertyEditorCollection,
            IOptions<AutoBlockListSettings> dataBlockConverterSettings,
            IConfigurationEditorJsonSerializer configurationEditorJsonSerializer)
        {
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

        public IEnumerable<CustomDisplayDataType> GetAllNCDataTypes(string alias)
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

        public string TransferContent(IProperty property, string? culture = null)
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