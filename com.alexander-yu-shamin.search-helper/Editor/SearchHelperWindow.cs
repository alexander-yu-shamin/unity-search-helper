using System;
using System.Linq;
//using Search.Helper.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor
{
    public partial class SearchHelperWindow : EditorWindow

    {
        private enum Panel
        {
            Uses,
            FindByGuid,
            UsedBy,
            Duplicate,
            Unused,
            UsesInBuild,
            AssetsByType
        }

        private const string WindowTitle = "Search Helper Tool";
        private const string WindowMenuItemName = "Window/Search/Open Search Helper Tool";
        private const string ContextMenuItemFindUsesName = "Assets/Search Helper Tool: Find Uses";
        private const string ContextMenuFindUsedByItemName = "Assets/Search Helper Tool: Find Used By";
        private const string ResourceString = "/Resources/";
        private const string EditorString = "/Editor/";


        private string[] PanelNames { get; set; }
        private Color ErrorColor => Color.red;
        private Color WarningColor => Color.yellow;



        [MenuItem(WindowMenuItemName)]
        public static SearchHelperTool OpenWindow()
        {
            return GetWindow<SearchHelperTool>(WindowTitle);
        }

        [MenuItem(ContextMenuItemFindUsesName)]
        public static void ShowUses()
        {
            //var window = OpenWindow().ChangePanel(Panel.Uses);
            //window?.SetCurrentObject(window?.FindDependencies(Selection.activeObject));
        }

        [MenuItem(ContextMenuFindUsedByItemName)]
        public static void ShowUsesBy()
        {
            //var window = OpenWindow().ChangePanel(Panel.UsedBy);
            //window?.SetCurrentObjects(window?.FindUsedBy(Selection.activeObject, window.ShouldFindDependencies),
            //    Selection.activeObject);
        }

        [MenuItem(ContextMenuItemFindUsesName, true)]
        [MenuItem(ContextMenuFindUsedByItemName, true)]
        public static bool ValidateActiveSelectedObject()
        {
            return Selection.activeObject;
        }

        public SearchHelperWindow()
        {
            //PanelNames = Enum.GetNames(typeof(Panel)).Select(element => element.AddSpacesBeforeUppercase()).ToArray();
        }
    }
}
