using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SearchHelper.Editor.Tools;
using UnityEditor;
using UnityEngine;
using Toolkit.Runtime.Extensions;

namespace SearchHelper.Editor
{
    public partial class SearchHelperWindow : EditorWindow
    {
        enum ToolType
        {
            DependencyTool = 0,
            UsedByTool,
            FindByGuidTool
        }

        private ToolType SelectedToolType { get; set; } = ToolType.DependencyTool;

        private Dictionary<ToolType, ToolBase> ToolMap { get; set; } = new()
        {
            { ToolType.DependencyTool, new DependenciesTool() },
            { ToolType.UsedByTool, new UsedByTool() },
            { ToolType.FindByGuidTool, new FindByGuidTool() },
        };

        [MenuItem(SearchHelperSettings.WindowMenuItemName)]
        private static SearchHelperWindow OpenWindow()
        {
            return GetWindow<SearchHelperWindow>(SearchHelperSettings.WindowTitle);
        }

        [MenuItem(SearchHelperSettings.ContextMenuItemFindDependenciesName, true)]
        [MenuItem(SearchHelperSettings.ContextMenuFindUsedByItemName, true)]
        public static bool ValidateActiveSelectedObject()
        {
            return Selection.activeObject;
        }

        [MenuItem(SearchHelperSettings.ContextMenuItemFindDependenciesName)]
        public static void ShowDependencies()
        {
            OpenWindow().SelectTool(ToolType.DependencyTool)?.Run(Selection.activeObject);
        }

        [MenuItem(SearchHelperSettings.ContextMenuFindUsedByItemName)]
        public static void ShowUsesBy()
        {
            OpenWindow().SelectTool(ToolType.UsedByTool)?.Run(Selection.activeObject);
        }

        [MenuItem(SearchHelperSettings.ContextMenuShowObjectGuidItemName)]
        public static void ShowObjectGuid()
        {
            OpenWindow().SelectTool(ToolType.FindByGuidTool)?.Run(Selection.activeObject);
        }

        public void OnGUI()
        {
            if (ToolMap.IsNullOrEmpty())
            {
                return;
            }

            var newToolType = (ToolType) GUILayout.SelectionGrid((int)SelectedToolType, ToolMap.Keys.Select(v => v.ToString()).ToArray(), ToolMap.Keys.Count);
            EditorGUILayout.Space(10);
            SelectTool(newToolType)?.Draw(position);
        }

        private ToolBase SelectTool(ToolType toolType)
        {
            SelectedToolType = toolType;
            return ToolMap.TryGetValue(toolType, out var tool) ? tool : null;
        }
    }
}
