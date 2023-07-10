using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
using System.Collections.Generic;
using DataBlockConverter.Core.Dtos;
using System.ComponentModel.DataAnnotations;

namespace DataBlockConverter.Core.Services
{
    public interface IDataBlockConverterService
    {
        void TransferContent(int id);
        string GetNameFormatting();
        string GetAliasFormatting();
        IDataType? CreateBLDataType(IDataType ncDataType);
        IEnumerable<CustomDisplayDataType> GetAllNCDataTypes();
    }
}
