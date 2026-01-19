using System.IO;
using UnityEngine;

namespace SearchHelper.Editor.Core
{
    public class SearchHelperSettings
    {
        public const string WindowTitle = "Search Helper Tool";
        public const string WindowMenuItemName = "Window/Search/Open Search Helper Tool";

        public const string ContextMenuBase = "Assets/Search Helper Tool/";


        public const string OpenWindowsName = "Open Search Helper Tool";
        public const string FindDependenciesToolName = "Find Dependencies";
        public const string FindUsedByToolName = "Find Used By";
        public const string FindByGuidToolName = "Find by GUID";
        public const string FindUnusedToolName = "Find Unused";
        public const string FindDuplicatesToolName = "Find Duplicates";
        public const string MergeToolName = "Add to Merge Tool";

        public const string ContextMenuItemOpenWindowName = ContextMenuBase + OpenWindowsName;
        public const string ContextMenuItemFindDependenciesName = ContextMenuBase + FindDependenciesToolName;
        public const string ContextMenuFindUsedByItemName = ContextMenuBase + FindUsedByToolName;
        public const string ContextMenuShowObjectGuidItemName = ContextMenuBase + FindByGuidToolName;
        public const string ContextMenuFindUnusedGlobalItemName = ContextMenuBase + FindUnusedToolName + " (Global)";
        public const string ContextMenuFindUnusedLocalItemName = ContextMenuBase + FindUnusedToolName + " (Local)";
        public const string ContextMenuFindDuplicatesItemName = ContextMenuBase + FindDuplicatesToolName;
        public const string ContextMenuMergeItemName= ContextMenuBase + MergeToolName;
    }
}
