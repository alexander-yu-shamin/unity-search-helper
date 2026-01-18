using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.Tools;
using UnityEditor;
using UnityEngine;
using Toolkit.Runtime.Extensions;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor.UI
{
    public class SearchHelperWindow : EditorWindow
    {
        public enum ToolType
        {
            DependencyTool = 0,
            UsedByTool,
            FindByGuidTool,
            UnusedTool,
            DuplicatesTool,
            MergeTool
        }

        private ToolType SelectedToolType { get; set; } = ToolType.DependencyTool;

        private Dictionary<ToolType, ToolBase> ToolMap { get; set; } = new()
        {
            { ToolType.DependencyTool, new DependenciesTool() },
            { ToolType.UsedByTool, new UsedByTool() },
            { ToolType.FindByGuidTool, new FindByGuidTool() },
            { ToolType.UnusedTool, new UnusedTool() },
            { ToolType.DuplicatesTool, new DuplicatesTool() },
            { ToolType.MergeTool, new MergeTool() },
        };

        private static Object SelectedObject => !Selection.assetGUIDs.IsNullOrEmpty()
            ? AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs.First()))
            : null;

        [MenuItem(SearchHelperSettings.WindowMenuItemName)]
        private static SearchHelperWindow OpenWindow()
        {
            return GetWindow<SearchHelperWindow>(SearchHelperSettings.WindowTitle);
        }

        [MenuItem(SearchHelperSettings.ContextMenuItemFindDependenciesName, true)]
        [MenuItem(SearchHelperSettings.ContextMenuFindUsedByItemName, true)]
        [MenuItem(SearchHelperSettings.ContextMenuFindDuplicatesItemName, true)]
        [MenuItem(SearchHelperSettings.ContextMenuShowObjectGuidItemName, true)]
        public static bool ValidateActiveSelectedObject()
        {
            return SelectedObject != null;
        }

        [MenuItem(SearchHelperSettings.ContextMenuItemFindDependenciesName)]
        public static void ShowDependencies()
        {
            OpenWindow().SelectTool(ToolType.DependencyTool)?.Run(SelectedObject, null);
        }

        [MenuItem(SearchHelperSettings.ContextMenuFindUsedByItemName)]
        public static void ShowUsesBy()
        {
            OpenWindow().SelectTool(ToolType.UsedByTool)?.Run(SelectedObject, null);
        }

        [MenuItem(SearchHelperSettings.ContextMenuShowObjectGuidItemName)]
        public static void ShowObjectGuid()
        {
            OpenWindow().SelectTool(ToolType.FindByGuidTool)?.Run(SelectedObject, null);
        }

        [MenuItem(SearchHelperSettings.ContextMenuFindUnusedGlobalItemName)]
        public static void FindUnusedObjectsGlobal()
        {
            OpenWindow().SelectTool(ToolType.UnusedTool)?.Run(SelectedObject, new () {IsGlobal = true });
        }

        [MenuItem(SearchHelperSettings.ContextMenuFindUnusedLocalItemName)]
        public static void FindUnusedObjectsLocal()
        {
            OpenWindow().SelectTool(ToolType.UnusedTool)?.Run(SelectedObject, new () { IsGlobal = false });
        }

        [MenuItem(SearchHelperSettings.ContextMenuFindDuplicatesItemName)]
        public static void FindDuplicates()
        {
            OpenWindow().SelectTool(ToolType.DuplicatesTool)?.Run(SelectedObject, null);
        }

        [MenuItem(SearchHelperSettings.ContextMenuMergeItemName)]
        public static void MergeFiles()
        {
            OpenWindow().SelectTool(ToolType.MergeTool)?.Run(SelectedObject, null);
        }

        public static void TransferToTool(ToolType toolType, List<ObjectContext> contexts)
        {
            OpenWindow()?.SelectTool(toolType).GetDataFromAnotherTool(contexts);
        }

        public void OnGUI()
        {
            if (ToolMap.IsNullOrEmpty())
            {
                return;
            }

            var newToolType = (ToolType) GUILayout.SelectionGrid((int)SelectedToolType, ToolMap.Keys.Select(v => v.ToString().ToSpacedWords()).ToArray(), ToolMap.Keys.Count);
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
