using UnityEngine;

namespace SearchHelper.Editor.UI
{
    public class UISettings
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

        public const float ToolLineHeight = 20.0f;
        public const float ToolLinePadding = 5.0f;
        public const float ToolLineWithPadding = ToolLineHeight + ToolLinePadding;
        public const float ToolButtonMinimalWidth = 120.0f;

        public static readonly Color RectBoxColor = new Color(0.0f, 0.0f, 0.0f, 0.2f);
        public static readonly Color RectBoxEmptyColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        public static readonly Color ErrorColor = Color.red;
        public const float RowHeight = 20.0f;
        public const float RowPadding = 2f;
        public const float ContentHeight = RowHeight;
        public const float ContentPadding = RowPadding;
        public const float ContentHeightWithPadding = ContentHeight + ContentPadding;
        public const float HorizontalIndent = 15.0f;
        public const float FirstElementIndent = 4.0f;
        public const float ScrollBarWidth = 16.0f;
        public const float NoScrollBarWidth = 4.0f;
        public const float GuidTextAreaWidth = 275.0f;
        public const float StateTextAreaWidth = 180.0f;
        public const float ExtraHeightToPreventBlinking = ContentHeightWithPadding * 5;

        public const float ObjectMinimalWidth = 200.0f;

        // Header
        public const float HeaderHeight = ContentHeight;
        public const float HeaderPadding = 6.0f;
        public const float HeaderHeightWithPadding = ContentHeight + HeaderPadding * 2;
        public const float HeaderSpace = 4.0f;



        public const float SelectButtonWidth = 65.0f;
        public const float RemoveButtonWidth = 60.0f;
        public const float MergeButtonWidth = 40.0f;

        // LogView
        public const float LogViewIndent = 5.0f;
        public const float LogViewPadding = 5.0f;
        public const float LogViewHeight = RowHeight;
        
        // DropdownButtons


        public const string FolderIconName = "d_Folder Icon";
        public const string InspectorIconName = "d_UnityEditor.InspectorWindow";
        public const string HierarchyIconName = "d_UnityEditor.SceneHierarchyWindow";
        public const string SceneHierarchySearchReferenceFormat = "ref:{0}";
    }
}
