# Search Helper

![search-helper-used-by-tool](com.alexander-yu-shamin.search-helper/Documentation~/images/search-helper-used-by-tool.png)

A lightweight multi-tool for asset management:
- [**Dependency Tool**](README.md#Dependency%20Tool): Maps asset relationships
- [**Used By Tool**](README.md#Used%20By%20Tool): Tracks object references
- [**Find By GUID Tool**](README.md#Find%20By%20GUID%20Tool): Locates assets by identifier
- [**Unused Tool**](README.md#Unused%20Tool): Identifies orphaned assets
- [**Duplicates Tool**](README.md#Duplicates%20Tool): Finds redundant copies
- [**Merge Tool**](README.md#Merge%20Tool): Consolidates duplicates

The tool can display any number of items with filtering and sorting capabilities.

## Dependency Tool

The `EditorUtility.CollectDeepHierarchy` method is used to compile all dependencies of a given object.

The tool doesn't check for direct script references (aka by filename from a script).

## Used By Tool

The `AssetDatabase.GetDependencies` method is used to a dependency map and tracking object references.

The tool doesn't check for direct script references (aka by filename from a script).

## Find By GUID Tool

The tool can locate objects by their GUID or display an object's GUID.

## Unused Tool

Similar to 'Used By', but scans all files within a folder instead of searching for dependencies on the folder itself.

The tool operates in two modes: Local and Global.
- **Global Mode**: Scans the entire project for dependencies
- **Local Mode**: Analyzes dependencies only within the specified folder

## Duplicates Tool

The tool identifies duplicates by comparing file hashes in a folder (defaults to "Assets"). The results can be transfered to "Merge Tool" by the button "Open in Merge Tool".

## Merge Tool

For meta-files, it compares hashes while ignoring the first two lines, with results color-coded in red and green for easy distinction.

![search-helper-merge-tool](com.alexander-yu-shamin.search-helper/Documentation~/images/search-helper-merge-tool.png)

