# Changelog

## [Unreleased]

## [0.0.9] - 2026-1-20

### Added
- Settings Menu Button
  - File size can be shown for every tool

### Fixed
- To avoid redundant processing, the data is refreshed in place rather than requiring a separate/tool re-run.

### Changed
- For better scalability, make your UI reactive to state changes rather than directly coupled to data mutations. This pattern is a lifesaver in large projects.- UI update not a data, but data state
- Unused Tool can show used elements (simular to Used By)

## [0.0.8] - 2026-1-19

### Added
- Some comments for better understanding
- Global and Local mode for unused files
- Natural sorting order
- The "Report" button to the Unused tools
- Filter by path, name, type
- Ignored rules

### Fixed
- The 'Merge' Tool replace GUIDs in meta-files 
- Fixed the Unused files search algo. It has errors
- Fixed the dependency search
- Added `AssetDatabase.SaveAssets` before calling another code from `AssetDatabase`

### Changed
- Added a cache to improve UI.


## [0.0.6] - 2026-1-07

### Added
- some comments for better understanding

### Fixed
- the 'Merge' Tool replace GUIDs in meta-files 

### Removed
- unnecessary things (Runtime and so on)

## [0.0.5] - 2025-10-05

## Added 

- 'Merge' tool

## [0.0.4] - 2025-09-17

## Added

- 'Duplicates' tool

## [0.0.3] - 2025-09-17

## Added

- 'Unused' tool - the same as 'Used by' but can be used for folders

## Fixed

- displaying a context with empty dependencies

## [0.0.2] - 2025-09-14

### Added

- 'Used By' tool
- 'Find By Guid' tool
- Context menu for the object field
  - Ping Object in the project window
  - Open property for the object
  - Open in folder
  - Find in scene
- UI has been updated

## [0.0.1] - 2025-09-11

### Added

- Dependency tool
- Virtual Scroll
- Main window with an abstract tool
- Sorting by name and path
- Filtering by path
