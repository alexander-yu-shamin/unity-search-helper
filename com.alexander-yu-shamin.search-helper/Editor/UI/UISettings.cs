using UnityEngine;

namespace SearchHelper.Editor.UI
{
    public class UISettings
    {
        // Names
        public const string WindowTitle = "Search Helper Tool";
        public const string WindowMenuItemName = "Window/Search/Open Search Helper Tool";

        public const string ContextMenuBase = "Assets/Search Helper Tool/";

        public const string OpenWindowsName = "Open Search Helper Tool";
        public const string FindDependenciesToolName = "Find Dependencies";
        public const string FindUsedByToolName = "Find Used By";
        public const string FindByGuidToolName = "Find by GUID";
        public const string FindUnusedToolName = "Find Unused";
        public const string FindDuplicatesToolName = "Find Duplicates";
        public const string MissingToolName = "Find Missing";
        public const string MergeToolName = "Add to Merge Tool";

        public const string ContextMenuItemOpenWindowName = ContextMenuBase + OpenWindowsName;
        public const string ContextMenuItemFindDependenciesName = ContextMenuBase + FindDependenciesToolName;
        public const string ContextMenuFindUsedByItemName = ContextMenuBase + FindUsedByToolName;
        public const string ContextMenuShowObjectGuidItemName = ContextMenuBase + FindByGuidToolName;
        public const string ContextMenuFindUnusedGlobalItemName = ContextMenuBase + FindUnusedToolName + " (Global)";
        public const string ContextMenuFindUnusedLocalItemName = ContextMenuBase + FindUnusedToolName + " (Local)";
        public const string ContextMenuFindDuplicatesItemName = ContextMenuBase + FindDuplicatesToolName;
        public const string ContextMenuFindMissingItemName = ContextMenuBase + MissingToolName;
        public const string ContextMenuMergeItemName = ContextMenuBase + MergeToolName;

        // General
        public const float Height = 20.0f;
        public const float ExtraHeightToPreventBlinking = Height * 5;

        // Tool
        public const float ToolLineHeight = Height;
        public const float ToolButtonMinimalWidth = 80.0f;

        // Header
        public const float HeaderHeight = Height;
        public const float HeaderPadding = 6.0f;
        public const float HeaderSpace = 4.0f;
        public const float FilterStringWidth = 250.0f;

        // Common
        public const float CommonLeftIndent = 2.0f;
        public const float CommonGuidWidth = 275.0f;
        public const float CommonGuidTextWidth = 40.0f;
        public const float CommonScrollBarWidth = 16.0f;
        public const float CommonNoScrollBarWidth = 4.0f;

        // Asset Header
        public const float AssetHeaderHeight = Height;
        public const float AssetHeaderPadding = 6.0f;
        public const float AssetHeaderHeightWithPadding = Height + HeaderPadding;
        public const float AssetHeaderFirstElementIndent = 4.0f;
        public const float AssetHeaderSpace = 4.0f;
        public const float AssetHeaderObjectWidth = 300.0f;
        public const float AssetHeaderDependencyCountWidth = 50.0f;
        public const float AssetHeaderDependencyCountTextWidth = 90.0f;
        public const float AssetHeaderSizeWidth = 70.0f;
        public const float AssetHeaderSizeTextWidth = 40.0f;
        public const float AssetHeaderSeparator = 2.0f;

        //  Merge Asset Header 
        public const float SelectButtonWidth = 65.0f;
        public const float BaseButtonWidth = 65.0f;
        public const float RemoveButtonWidth = 60.0f;
        public const float DiffButtonWidth = 40.0f;

        // Dependency
        public const float DependencyFirstElementIndent = 4.0f;
        public const float DependencyHeight = Height;
        public const float DependencyPadding = 2.0f;
        public const float DependencyHeightWithPadding = DependencyHeight + DependencyPadding;
        public const float DependencySpace = 4.0f;
        public const float DependencyStateTextAreaWidth = 180.0f;

        // LogView
        public const float LogViewIndent = CommonLeftIndent;
        public const float LogViewPadding = 5.0f;
        public const float LogViewHeight = Height;
        
        // Colors
        public static readonly Color RectBoxColor = new Color(0.0f, 0.0f, 0.0f, 0.15f);
        public static readonly Color AssetHeaderRectBoxColor = new Color(0.0f, 0.0f, 0.0f, 0.15f);
        public static readonly Color SeparatorColor = new Color(0.0f, 1.0f, 1.0f, 0.0f);
        public static readonly Color ErrorColor = new Color(1f, 0.4f, 0.4f); // #FF6666
        public static readonly Color WarningColor = new Color(1f, 0.9f, 0.3f); // #FFE64D

        // Icons
        public const string FolderIconName = "d_Folder Icon";
        public const string InspectorIconName = "d_UnityEditor.InspectorWindow";
        public const string HierarchyIconName = "d_UnityEditor.SceneHierarchyWindow";

        // Formats
        public const string SceneHierarchySearchReferenceFormat = "ref:{0}";
    }
}
