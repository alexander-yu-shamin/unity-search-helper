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
            UnusedTool,
            DuplicatesTool,
            MergeTool,
            FindByGuidTool,
        }

        private static ToolType SelectedToolType { get; set; } = ToolType.DependencyTool;

        private Dictionary<ToolType, ToolBase> ToolMap { get; set; } = new()
        {
            { ToolType.DependencyTool, new DependenciesTool() },
            { ToolType.UsedByTool, new UsedByTool() },
            { ToolType.UnusedTool, new UnusedTool() },
            { ToolType.DuplicatesTool, new DuplicatesTool() },
            { ToolType.MergeTool, new MergeTool() },
            { ToolType.FindByGuidTool, new FindByGuidTool() },
        };

        private static Object SelectedObject => !Selection.assetGUIDs.IsNullOrEmpty()
            ? AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs.First()))
            : null;

        [MenuItem(SearchHelperSettings.WindowMenuItemName)]
        [MenuItem(SearchHelperSettings.ContextMenuItemOpenWindowName, priority = 100)]
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

        [MenuItem(SearchHelperSettings.ContextMenuItemFindDependenciesName, priority = 111)]
        public static void ShowDependencies()
        {
            OpenWindow().SelectTool(ToolType.DependencyTool)?.Run(SelectedObject);
        }

        [MenuItem(SearchHelperSettings.ContextMenuFindUsedByItemName, priority = 112)]
        public static void ShowUsesBy()
        {
            OpenWindow().SelectTool(ToolType.UsedByTool)?.Run(SelectedObject);
        }

        [MenuItem(SearchHelperSettings.ContextMenuFindUnusedGlobalItemName, priority = 113)]
        public static void FindUnusedObjectsGlobal()
        {
            var selectTool = OpenWindow().SelectTool(ToolType.UnusedTool);
            if (selectTool != null)
            {
                selectTool.IsGlobalScope = true;
                selectTool.Run(SelectedObject);
            }
        }

        [MenuItem(SearchHelperSettings.ContextMenuFindUnusedLocalItemName, priority = 114)]
        public static void FindUnusedObjectsLocal()
        {
            var selectTool = OpenWindow().SelectTool(ToolType.UnusedTool);
            if (selectTool != null)
            {
                selectTool.IsGlobalScope = false;
                selectTool.Run(SelectedObject);
            }
        }

        [MenuItem(SearchHelperSettings.ContextMenuFindDuplicatesItemName, priority = 115)]
        public static void FindDuplicates()
        {
            OpenWindow().SelectTool(ToolType.DuplicatesTool)?.Run(SelectedObject);
        }

        [MenuItem(SearchHelperSettings.ContextMenuMergeItemName, priority = 116)]
        public static void MergeFiles()
        {
            OpenWindow().SelectTool(ToolType.MergeTool)?.Run(SelectedObject);
        }

        [MenuItem(SearchHelperSettings.ContextMenuShowObjectGuidItemName, priority = 117)]
        public static void ShowObjectGuid()
        {
            OpenWindow().SelectTool(ToolType.FindByGuidTool)?.Run(SelectedObject);
        }

        private void OnEnable()
        {
            SearchHelperService.OnAssetChanged += AssetChangedHandler;
        }

        private void OnDisable()
        {
            SearchHelperService.OnAssetChanged -= AssetChangedHandler;
        }

        public static void TransferToTool(ToolType toolType, ObjectContext context)
        {
            OpenWindow()?.SelectTool(toolType).GetDataFromAnotherTool(context);
        }
        public static void TransferToTool(ToolType toolType, IEnumerable<ObjectContext> contexts)
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

        private void AssetChangedHandler(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var tool in ToolMap)
            {
                tool.Value?.AssetChanged(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            }
        }
    }
}
