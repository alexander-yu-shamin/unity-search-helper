using System.Collections.Generic;
using System.IO;
using System.Linq;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
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

        public override void InnerDraw(Rect windowRect)
        {
            DrawHeaderLines(() =>
            {
                SelectedObject = DrawSelectedObject(SelectedObject);
                EGuiKit.Button("Find", Run, GUILayout.Width(50));
            });
            EGuiKit.Horizontal(() =>
            {


                //EGuiKit.Space();
                //EGuiKit.Color(Color.gray, () =>
                //{
                //    EGuiKit.Label("Similar to 'Used By', but scans all files within the folder");
                //});

                //EGuiKit.FlexibleSpace();
                //DrawHeaderControls();
            });

            DrawVirtualScroll(Assets);
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
                menu.AddDisabledItem(new GUIContent(prefix + "Remove Unused Items"));
            }
            else
            {
                menu.AddItem(new GUIContent(prefix + "Remove Unused Items"), false, RemovedUnusedItems);
            }
        }

        private List<Asset> FindUnused(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

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
            return assets;
        }

        private void RemovedUnusedItems()
        {
            if (Assets.IsNullOrEmpty())
            {
                return;
            }

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

                File.Delete(asset.Path);
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
    }
}
