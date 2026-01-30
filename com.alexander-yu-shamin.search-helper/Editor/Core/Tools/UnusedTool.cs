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

        protected override string EmptyObjectContextText
        {
            get
            {
                if (AreScopeRulesSupported)
                {
                    if (IsGlobalScope)
                    {
                        return "This object is not referenced anywhere in the project.";
                    }
                    else
                    {
                        return "This object is not referenced locally";
                    }
                }
                else
                {
                    return "This object is not referenced anywhere in the project.";
                }
            }
        } 

        private Object SelectedObject { get; set; }
        private List<Asset> Assets { get; set; }
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.UnusedTool;

        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() =>
            {
                SelectedObject = DrawObject(SelectedObject);
                EGuiKit.Button("Find", Run);

                EGuiKit.Space();
                EGuiKit.Color(Color.gray, () =>
                {
                    EGuiKit.Label("Similar to 'Used By', but scans all files within the folder");
                });

                EGuiKit.FlexibleSpace();
                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Assets));
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

        protected override void AddActionContextMenu(GenericMenu menu)
        {
            if (Assets.IsNullOrEmpty())
            {
                menu.AddDisabledItem(new GUIContent("Remove Unused Items"));
            }
            else
            {
                menu.AddItem(new GUIContent("Remove Unused Items"), false, RemovedUnusedItems);
            }
        }

        private List<Asset> FindUnused(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var map = FolderOrFile(obj).Select(Asset.ToObjectContext).ToDictionary(key => key.Path);

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
            UpdateAssets(assets);
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
