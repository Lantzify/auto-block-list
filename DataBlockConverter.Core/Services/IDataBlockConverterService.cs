using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DataBlockConverter.Core.Services
{
    public interface IDataBlockConverterService
    {
        IDataType? CreateBLDataType(IDataType ncDataType);
    }
}
