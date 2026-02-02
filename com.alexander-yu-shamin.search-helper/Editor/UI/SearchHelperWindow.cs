using System.Collections.Generic;
using System.Linq;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.Tools;
using Toolkit.Editor.Helpers.IMGUI;
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

        public static bool IsFullScreenMode { get; set; } = true;
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

        [MenuItem(UISettings.WindowMenuItemName)]
        [MenuItem(UISettings.ContextMenuItemOpenWindowName, priority = 100)]
        private static SearchHelperWindow OpenWindow()
        {
            return GetWindow<SearchHelperWindow>(UISettings.WindowTitle);
        }

        [MenuItem(UISettings.ContextMenuItemFindDependenciesName, true)]
        [MenuItem(UISettings.ContextMenuFindUsedByItemName, true)]
        [MenuItem(UISettings.ContextMenuFindDuplicatesItemName, true)]
        [MenuItem(UISettings.ContextMenuShowObjectGuidItemName, true)]
        public static bool ValidateActiveSelectedObject()
        {
            return SelectedObject != null;
        }

        [MenuItem(UISettings.ContextMenuItemFindDependenciesName, priority = 111)]
        public static void ShowDependencies()
        {
            OpenWindow().SelectTool(ToolType.DependencyTool)?.Run(SelectedObject);
        }

        [MenuItem(UISettings.ContextMenuFindUsedByItemName, priority = 112)]
        public static void ShowUsesBy()
        {
            OpenWindow().SelectTool(ToolType.UsedByTool)?.Run(SelectedObject);
        }

        [MenuItem(UISettings.ContextMenuFindUnusedGlobalItemName, priority = 113)]
        public static void FindUnusedObjectsGlobal()
        {
            var selectTool = OpenWindow().SelectTool(ToolType.UnusedTool);
            if (selectTool != null)
            {
                selectTool.IsGlobalScope = true;
                selectTool.Run(SelectedObject);
            }
        }

        [MenuItem(UISettings.ContextMenuFindUnusedLocalItemName, priority = 114)]
        public static void FindUnusedObjectsLocal()
        {
            var selectTool = OpenWindow().SelectTool(ToolType.UnusedTool);
            if (selectTool != null)
            {
                selectTool.IsGlobalScope = false;
                selectTool.Run(SelectedObject);
            }
        }

        [MenuItem(UISettings.ContextMenuFindDuplicatesItemName, priority = 115)]
        public static void FindDuplicates()
        {
            OpenWindow().SelectTool(ToolType.DuplicatesTool)?.Run(SelectedObject);
        }

        [MenuItem(UISettings.ContextMenuMergeItemName, priority = 116)]
        public static void MergeFiles()
        {
            OpenWindow().SelectTool(ToolType.MergeTool)?.Run(SelectedObject);
        }

        [MenuItem(UISettings.ContextMenuShowObjectGuidItemName, priority = 117)]
        public static void ShowObjectGuid()
        {
            OpenWindow().SelectTool(ToolType.FindByGuidTool)?.Run(SelectedObject);
        }

        private void OnEnable()
        {
            foreach (var (toolType, tool) in ToolMap)
            {
                tool.Init();
            }

            SearchHelperService.OnAssetChanged += AssetChangedHandler;
        }

        private void OnDisable()
        {
            SearchHelperService.OnAssetChanged -= AssetChangedHandler;
        }

        public static void TransferToTool(ToolType from, ToolType to, Asset context)
        {
            OpenWindow()?.SelectTool(to).GetDataFromAnotherTool(from, to, context);
        }

        public static void TransferToTool(ToolType from, ToolType to, IEnumerable<Asset> contexts)
        {
            OpenWindow()?.SelectTool(to).GetDataFromAnotherTool(from, to, contexts);
        }

        public void OnGUI()
        {
            if (ToolMap.IsNullOrEmpty())
            {
                return;
            }

            var rect = new Rect(0.0f, UISettings.ToolLineHeight, position.width, position.height - UISettings.ToolLineHeight);
            IsFullScreenMode = rect.width > UISettings.ToolButtonMinimalWidth * ToolMap.Count;
            if (IsFullScreenMode)
            {
                var newToolType = (ToolType)GUILayout.SelectionGrid((int)SelectedToolType, ToolMap.Keys.Select(v => v.ToString().ToSpacedWords()).ToArray(), ToolMap.Keys.Count, GUILayout.Height(UISettings.ToolLineHeight));
                SelectTool(newToolType);
            }
            else
            {
                SelectTool((ToolType)EditorGUILayout.EnumPopup(SelectedToolType, GUILayout.Height(UISettings.ToolLineHeight)));
            }

            SelectTool(SelectedToolType)?.Draw(rect);
        }

        private ToolBase SelectTool(ToolType toolType)
        {
            SelectedToolType = toolType;
            return ToolMap.GetValueOrDefault(toolType);
        }

        private void AssetChangedHandler(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            SelectTool(SelectedToolType)?.AssetChanged(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}
