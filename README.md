# Auto block list
Auto block list is a Umbraco package made for v10+. Made to help automate the process of converting nested content into a block list. And transfering the content.

## About
With the removal of nested content in Umbraco 13. Uppgrading can become a bit of a nightmere if you frequently use it. That's where Auto block list comes in. With one click AutoBlockLists runs the following workflow.
- Creates the block list data type.
- Adds the new data type to the document type.
- And lastly it transfers the content to the newly created block list

### Settings
```
     "AutoBlockList": {
        NameFormatting: "[Block list] - {0}",
        AliasFormatting: "{0}BL"
    }
```

## Contributing

If you would like to help me improve this package, feel free to create a pull request!

## Issues

If you find any issues with the package feel free to raise a issue!
