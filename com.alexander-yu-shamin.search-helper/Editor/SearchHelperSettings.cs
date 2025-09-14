using UnityEngine;

namespace SearchHelper.Editor
{
    public class SearchHelperSettings
    {
        
        public const string WindowTitle = "Search Helper Tool";
        public const string WindowMenuItemName = "Window/Search/Open Search Helper Tool";
        public const string ContextMenuItemFindDependenciesName = "Assets/Search Helper Tool: Find Dependencies";
        public const string ContextMenuFindUsedByItemName = "Assets/Search Helper Tool: Find Used By";
        public const string ContextMenuShowObjectGuidItemName = "Assets/Search Helper Tool: Show Object GUID";
        public const string ResourceString = "/Resources/";
        public const string EditorString = "/Editor/";

        public const string DependenciesToolName = "Dependencies";
        public const string UsedByToolName = "Used By";
        public const string FindByGuidToolName = "Find by GUID";

        public const string UsesToolButtonText = "Find Dependencies";
        public const string FindByGuidButtonText = "Find by GUID";

        public Color ErrorColor => Color.red;
        public Color WarningColor => Color.yellow;

    }
}
