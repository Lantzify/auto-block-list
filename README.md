# Auto block list
![Version](https://img.shields.io/nuget/v/AutoBlockList?label=version)
[![Nuget](https://img.shields.io/nuget/dt/AutoBlockList?color=2346c018&logo=Nuget)](https://www.nuget.org/packages/AutoBlockList/)

> [!NOTE]  
> 2.0.0 introduced the ability to convert macros!

Auto block list is an Umbraco package made for v10+. Made to help automate the process of converting nested content and macros into block list components. In addition, it transfers the content to the new block list format.

## About
With the removal of nested content in Umbraco 13 and macros in Umbraco 14, upgrading can potentially be challenging if you use these features frequently. That's where Auto block list comes in. With one click, AutoBlockList runs the following workflow based on data type.

### Nested Content Conversion
- Creates the block list data type based on the old nested content data type.
- Adds the new data type to the document type.
- Transfers the existing content to the newly created block list.


### Macro Conversion
1. Scans all content with TinyMCE properties for macro usage
2. For each unique macro found:
   - Creates an element-type document type with the same alias as the macro
   - Adds properties matching the macro's parameters
   - Places the new document type in a dedicated folder
3. Converts macro instances in content to block list components
4. Migrates partial view macros to regular partial views
5. Updates the rich text editor configuration to include the new block types


## Settings
```
"AutoBlockList": {
     BlockListEditorSize: "medium",
     SaveAndPublish: true,
     NameFormatting: "[Block list] - {0}",
     AliasFormatting: "{0}BL",
     FolderNameForContentTypes: "[Rich text editor] - Components"
}
```
- ``BlockListEditorSize`` Determines the default size when creating a block list data type. Sizes: ``small``, ``medium``, ``large``.
- ``SaveAndPublish`` When transferring content. If the node should be saved and published or only saved.
- In the ``NameFormatting`` setting the ``{0}`` will be replaced with the nested content data type name. Make sure to keep the ``{0}``. 
- In the ``AliasFormatting`` setting the ``{0}`` will be replaced with the property alias containing the nested content. Make sure to keep the ``{0}``. 
- ``FolderNameForContentTypes`` Determines the name of the folder where document types based on macros will be created. (This folder will be created in the root)

## Usage

1. **Install the package** and restart your Umbraco application
2. **Navigate to Settings** and find the "Auto Block List" section
3. **Configure settings** in your `appsettings.json` if needed (optional)
4. **Review content** in the overview dashboard
5. **Select items** to convert (nested content and/or macro content)
6. **Start conversion** and monitor progress
7. **Review results** in the detailed report

## Contributing

If you would like to help me improve this package, feel free to create a pull request!

## Issues

If you find any issues with the package feel free to raise an issue!

## Screenshots
![Macro demo](assets/macroDemo.gif "Demo")
![Demo](assets/demo.gif "Demo")
![Dashboard](assets/dashboard.PNG "Dashboard")
![Select](assets/select.PNG "Select")
![Usure](assets/usure.PNG "Usure")
![Result](assets/result.PNG "Result")
![Done](assets/done.PNG "Done")
