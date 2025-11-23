using Umbraco.Cms.Core;
using AutoBlockList.Dtos;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;

namespace AutoBlockList.Services.interfaces
{
    public interface IAutoBlockListMacroService
	{
		string GetFolderNameForContentTypes();
		string[] GetMacroStrings(string content);
		Dictionary<string, object> GetParametersFromMaco(string macroString);
		bool HasMacro(string content);
		ContentType ConvertMacroToContentType(IMacro macro, out List<ConvertReport> reports);
		ConvertReport CreatePartialView(IMacro macro);
		RichTextEditorValue ReplaceMacroWithBlockList(string macroString, Dictionary<string, object> parameters, RichTextConfigurationEditor configEditor, IDataType dataType, ContentType contentType, RichTextEditorValue richTextEditorValue);
	}
}