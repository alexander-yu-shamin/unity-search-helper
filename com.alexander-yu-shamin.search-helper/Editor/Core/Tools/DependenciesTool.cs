using System.Collections.Generic;
using System.Linq;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class DependenciesTool : ToolBase
    {
        private Object SelectedObject { get; set; }
        private List<Asset> Assets { get; set; }
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.DependencyTool;

        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() =>
            {
                SelectedObject = DrawObject(SelectedObject);

                EGuiKit.Button("Find", Run);
                EGuiKit.FlexibleSpace();
                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Assets));
        }

        public override void Run(Object selectedObject)
        {
            if (selectedObject == null)
            {
                Debug.LogError($"Selected Object is null!");
                return;
            }

            SelectedObject = selectedObject;
            Run();
        }

        public override void Run()
        {
            Assets = FindDependencies(SelectedObject);
        }

        public override void GetDataFromAnotherTool(SearchHelperWindow.ToolType from, SearchHelperWindow.ToolType to, Asset asset)
        {
            Run(asset.Object);
        }

        protected override string GetEmptyAssetText(Asset mainAsset)
        {
            if (mainAsset.IsFolder)
            {
                return "Folders are not supported.";
            }
            else
            {
                return "The asset has no dependencies.";
            }
        }

        private List<Asset> FindDependencies(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var assets = FolderOrFile(obj).Select(SearchHelperService.FindDependencies).ToList();
            UpdateAssets(assets, forceUpdate: true);
            return assets;
        }
    }
}
