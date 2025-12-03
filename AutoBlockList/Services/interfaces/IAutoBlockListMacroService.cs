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
		string ProcessTinyMceContentForMacroConversion(string tinyMceContent, IPropertyType tinyMceDataType, string culture = null);
		bool ProcessContentForMacroConversion(IContent content, IPropertyType tinyMceDataType, string culture = null);
		ContentType ConvertMacroToContentType(IMacro macro);
		ConvertReport CreatePartialView(IMacro macro);
		RichTextEditorValue ReplaceMacroWithBlockList(string macroString, Dictionary<string, object> parameters, RichTextConfigurationEditor configEditor, IDataType dataType, ContentType contentType, RichTextEditorValue richTextEditorValue);
	}
}