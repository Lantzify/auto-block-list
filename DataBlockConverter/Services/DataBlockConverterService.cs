using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
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
        private readonly IDataTypeService _dataTypeService;
        private readonly IContentTypeService _contentTypeService;
        
        private readonly IDataValueEditorFactory _dataValueEditorFactory;
        private readonly PropertyEditorCollection _propertyEditorCollection;
        private readonly IOptions<DataBlockConverterSettings> _dataBlockConverterSettings;
        private readonly IConfigurationEditorJsonSerializer _configurationEditorJsonSerializer;

        public DataBlockConverterService(IDataTypeService dataTypeService,
            IContentTypeService contentTypeService,
            IDataValueEditorFactory dataValueEditorFactory,
            PropertyEditorCollection propertyEditorCollection,
            IOptions<DataBlockConverterSettings> dataBlockConverterSettings,
            IConfigurationEditorJsonSerializer configurationEditorJsonSerializer)
        {
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

        public string GetNameFormatting()
        {
            return _dataBlockConverterSettings.Value.NameFormatting;
        }
    }
}
