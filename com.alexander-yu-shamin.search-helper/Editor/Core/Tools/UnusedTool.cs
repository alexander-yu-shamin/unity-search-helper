using System.Collections.Generic;
using System.IO;
using System.Linq;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Core.Tools
{
    public class UnusedTool : ToolBase
    {
        protected override bool AreScopeRulesSupported { get; set; } = true;
        protected override bool ShowAssetWithDependencies { get; set; } = false;
        protected override bool ShowSize { get; set; } = true;
        private Object SelectedObject { get; set; }
        private List<Asset> Assets { get; set; }
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.UnusedTool;

        public override void Init()
        {
            base.Init();
            Log(LogType.Log, $"[{(IsGlobalScope ? "Global" : "Local")}] Similar to 'Used By', but scans all files within the folder");
        }

        public override void InnerDraw(Rect windowRect)
        {
            DrawMain(firstLineLeft: () =>
            {
                EGuiKit.Horizontal(() =>
                {
                    SelectedObject = DrawSelectedObject(SelectedObject);
                    EGuiKit.Button("Find", Run);
                });
            }, drawContent: () =>
            {
                DrawVirtualScroll(Assets);
            });
        }

        public override void Run(Object selectedObject)
        {
            SelectedObject = selectedObject;
            Run();
        }

        public override void Run()
        {
            Assets = FindUnused(SelectedObject);
        }

        protected override void AddActionContextMenu(GenericMenu menu, string prefix)
        {
            if (Assets.IsNullOrEmpty())
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Clean Up Unused Items"));
            }
            else
            {
                menu.AddItem(new GUIContent(prefix + "Clean Up Unused Items"), false, RemovedUnusedItems);
            }
        }

        private List<Asset> FindUnused(Object obj)
        {
            if (obj == null)
            {
                Log(LogType.Error, "Choose an object to proceed.");
                return null;
            }

            Log(LogType.Warning, $"[{(IsGlobalScope ? "Global" : "Local")}] Scanning for unused assets...");
            var map = FolderOrFile(obj).Select(Asset.ToAsset).ToDictionary(key => key.Path);

            var root = IsGlobalScope ? null : FolderPathFromObject(obj);
            var dependencyMap = SearchHelperService.BuildDependencyMap(root, IsCacheUsed);
            foreach (var (path, context) in map)
            {
                if (dependencyMap.TryGetValue(path, out var dependencies))
                {
                    context.Dependencies = dependencies.ToList();
                }
            }

            var assets = map.Values.ToList();
            UpdateAssets(assets, forceUpdate: true);
            Log(LogType.Warning, $"[{(IsGlobalScope ? "Global" : "Local")}] Scanning ready.");
            return assets;
        }

        private void RemovedUnusedItems()
        {
            if (Assets.IsNullOrEmpty())
            {
                return;
            }

            Log(LogType.Warning, "Cleaning up unused items...");

            AssetDatabase.StartAssetEditing();
            foreach (var asset in Assets)
            {
                if (!IsMainAssetVisible(asset))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(asset.Path))
                {
                    continue;
                }

                Log(LogType.Warning, $"Delete: {asset.Path}");
                File.Delete(asset.Path);
            }

            Log(LogType.Warning, "Cleaning up ready.");
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
    }
}
