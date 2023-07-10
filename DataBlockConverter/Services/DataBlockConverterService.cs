using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Umbraco.Cms.Core;
using Umbraco.Extensions;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
using DataBlockConverter.Dtos;
using Umbraco.Cms.Core.Services;
using System.Collections.Generic;
using DataBlockConverter.Core.Dtos;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.PropertyEditors;
using static Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Services.Implement;
using System.ComponentModel.DataAnnotations;
using DataType = Umbraco.Cms.Core.Models.DataType;
using static Umbraco.Cms.Core.PropertyEditors.BlockListConfiguration;

namespace DataBlockConverter.Core.Services
{
    public class DataBlockConverterService : IDataBlockConverterService
    {
        private readonly IContentService _contentService;
        private readonly IDataTypeService _dataTypeService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataValueEditorFactory _dataValueEditorFactory;
        private readonly PropertyEditorCollection _propertyEditorCollection;
        private readonly IOptions<DataBlockConverterSettings> _dataBlockConverterSettings;
        private readonly IConfigurationEditorJsonSerializer _configurationEditorJsonSerializer;

        public DataBlockConverterService(IContentService contentService,
            IDataTypeService dataTypeService,
            IContentTypeService contentTypeService,
            IDataValueEditorFactory dataValueEditorFactory,
            PropertyEditorCollection propertyEditorCollection,
            IOptions<DataBlockConverterSettings> dataBlockConverterSettings,
            IConfigurationEditorJsonSerializer configurationEditorJsonSerializer)
        {
            _contentService = contentService;
            _dataTypeService = dataTypeService;
            _contentTypeService = contentTypeService;
            _dataValueEditorFactory = dataValueEditorFactory;
            _propertyEditorCollection = propertyEditorCollection;
            _dataBlockConverterSettings = dataBlockConverterSettings;
            _configurationEditorJsonSerializer = configurationEditorJsonSerializer;
        }

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
                    ValidationLimit = new BlockListConfiguration.NumberRange()
                    {
                        Max = ncConfig.MaxItems,
                        Min = ncConfig.MinItems
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
                    EditorSize = "Medium",
                    ContentElementTypeKey = _contentTypeService.Get(ncContentType.Alias).Key
                });
            }

            blConfig.Blocks = blocks.ToArray();

            return blDataType;
        }

        public IEnumerable<CustomDisplayDataType> GetAllNCDataTypes()
        {
            var dataTypes = new List<CustomDisplayDataType>();
            foreach (var dataType in _dataTypeService.GetAll().Where(x => x.EditorAlias == PropertyEditors.Aliases.NestedContent))
                dataTypes.Add(new CustomDisplayDataType()
                {
                    Id = dataType.Id,
                    Name = dataType.Name,
                    Icon = dataType.Editor.Icon,
                    MatchingBLId = _dataTypeService.GetDataType(string.Format(GetNameFormatting(), dataType.Name))?.Id
                });

            return dataTypes;
        }

        public void TransferContent(int id)
        {
            var node = _contentService.GetById(id);
            if (node == null)
                return;

            var allNC = node.Properties.Where(x => x.PropertyType.PropertyEditorAlias == PropertyEditors.Aliases.NestedContent);

            foreach (var nc in allNC)
            {
                var ncValues = JsonConvert.DeserializeObject<IEnumerable<Dictionary<string, string>>>(nc.GetValue().ToString());


                var contentData = new List<Dictionary<string, string>>();
                var contentUdiList = new List<Dictionary<string, string>>();

                string[] defaultNC = new string[]
                {
                 "name",
                 "ncContentTypeAlias",
                 "PropType",
                 "key"
                };

                foreach (var ncValue in ncValues)
                {
                    var rawContentType = ncValue.FirstOrDefault(x => x.Key == "ncContentTypeAlias");
                    var contentType = _contentTypeService.GetAllElementTypes().FirstOrDefault(x => x.Alias == rawContentType.Value);
                    var contentUdi = new GuidUdi("element", System.Guid.NewGuid()).ToString();
                    var values = ncValue.Where(x => !defaultNC.Contains(x.Key));

                    var content = new Dictionary<string, string>
                    {
                        {"contentTypeKey", contentType.Key.ToString() },
                        {"udi", contentUdi },
                    };

                    foreach (var value in values)
                        content.Add(value.Key, value.Value);

                    
                    contentData.Add(content);

                    contentUdiList.Add(new Dictionary<string, string>
                    {
                        {"contentUdi", contentUdi },
                    });
                }

                var blockList = new BlockList()
                {
                    layout = new BlockListUdi(contentUdiList, new List<Dictionary<string, string>>()),
                    contentData = contentData,
                    settingsData = new List<Dictionary<string, string>>()

                };

                node.SetValue(string.Format(GetAliasFormatting(), nc.Alias), JsonConvert.SerializeObject(blockList));
            }

            _contentService.Save(node);
        }

        public string GetNameFormatting() => _dataBlockConverterSettings.Value.NameFormatting;
        public string GetAliasFormatting() => _dataBlockConverterSettings.Value.AliasFormatting;
    }
}
