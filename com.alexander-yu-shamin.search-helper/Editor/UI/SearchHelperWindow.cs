using System.Collections.Generic;
using System.Linq;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.Core.Tools;
using Toolkit.Editor.Attributes;
using UnityEditor;
using UnityEngine;
using Toolkit.Runtime.Extensions;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor.UI
{
    public class SearchHelperWindow : EditorWindow, IEditorPrefs
    {
        public enum ToolType
        {
            Dependency = 0,
            UsedBy,
            Unused,
            Duplicates,
            Merge,
            Missing,
            FindByGuid,
        }

        public string EditorPrefsPrefix { get; } = nameof(SearchHelperWindow);

        [EditorPrefs]
        private bool? ForceFullScreenModePrefs { get; set; }

        [EditorPrefs]
        private ToolType SelectedToolTypePrefs { get; set; } = ToolType.Dependency;

        public static bool? ForceFullScreenMode { get; set; }
        public static bool IsFullScreenMode { get; set; } = true;

        private static ToolType SelectedToolType { get; set; } = ToolType.Dependency;

        private Dictionary<ToolType, ToolBase> ToolMap { get; set; } = new()
        {
            { ToolType.Dependency, new DependenciesTool() },
            { ToolType.UsedBy, new UsedByTool() },
            { ToolType.Unused, new UnusedTool() },
            { ToolType.Duplicates, new DuplicatesTool() },
            { ToolType.Merge, new MergeTool() },
            { ToolType.Missing, new MissingTool() },
            { ToolType.FindByGuid, new FindByGuidTool() },
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
        [MenuItem(UISettings.ContextMenuFindMissingItemName, true)]
        public static bool ValidateActiveSelectedObject()
        {
            return SelectedObject != null;
        }

        [MenuItem(UISettings.ContextMenuItemFindDependenciesName, priority = 111)]
        public static void ShowDependencies()
        {
            OpenWindow().SelectTool(ToolType.Dependency)?.Run(SelectedObject);
        }

        [MenuItem(UISettings.ContextMenuFindUsedByItemName, priority = 112)]
        public static void ShowUsesBy()
        {
            OpenWindow().SelectTool(ToolType.UsedBy)?.Run(SelectedObject);
        }

        [MenuItem(UISettings.ContextMenuFindUnusedGlobalItemName, priority = 113)]
        public static void FindUnusedObjectsGlobal()
        {
            var selectTool = OpenWindow().SelectTool(ToolType.Unused);
            if (selectTool != null)
            {
                selectTool.IsGlobalScope = true;
                selectTool.Run(SelectedObject);
            }
        }

        [MenuItem(UISettings.ContextMenuFindUnusedLocalItemName, priority = 114)]
        public static void FindUnusedObjectsLocal()
        {
            var selectTool = OpenWindow().SelectTool(ToolType.Unused);
            if (selectTool != null)
            {
                selectTool.IsGlobalScope = false;
                selectTool.Run(SelectedObject);
            }
        }

        [MenuItem(UISettings.ContextMenuFindDuplicatesItemName, priority = 115)]
        public static void FindDuplicates()
        {
            OpenWindow().SelectTool(ToolType.Duplicates)?.Run(SelectedObject);
        }

        [MenuItem(UISettings.ContextMenuMergeItemName, priority = 116)]
        public static void MergeFiles()
        {
            OpenWindow().SelectTool(ToolType.Merge)?.Run(SelectedObject);
        }

        [MenuItem(UISettings.ContextMenuFindMissingItemName, priority = 117)]
        public static void FindMissing()
        {
            OpenWindow().SelectTool(ToolType.Missing)?.Run(SelectedObject);
        }

        [MenuItem(UISettings.ContextMenuShowObjectGuidItemName, priority = 118)]
        public static void ShowObjectGuid()
        {
            OpenWindow().SelectTool(ToolType.FindByGuid)?.Run(SelectedObject);
        }

        private void OnEnable()
        {
            this.LoadSettings();
            foreach (var (toolType, tool) in ToolMap)
            {
                tool.Init();
            }

            ForceFullScreenMode = ForceFullScreenModePrefs;
            SelectedToolType = SelectedToolTypePrefs;
            SearchHelperService.OnAssetChanged += AssetChangedHandler;
        }

        private void OnDisable()
        {
            SearchHelperService.OnAssetChanged -= AssetChangedHandler;
            ForceFullScreenModePrefs = ForceFullScreenMode;
            SelectedToolTypePrefs = SelectedToolType;
            this.SaveSettings();
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

            if (ForceFullScreenMode.HasValue)
            {
                IsFullScreenMode = ForceFullScreenMode.Value;
            }

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
