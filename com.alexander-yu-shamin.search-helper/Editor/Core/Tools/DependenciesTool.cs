using System.Collections.Generic;
using System.Linq;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using UnityEngine;

namespace SearchHelper.Editor.Core.Tools
{
    public class DependenciesTool : ToolBase
    {
        private Object SelectedObject { get; set; }
        private List<Asset> Assets { get; set; }
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.DependencyTool;

        public override void Init()
        {
            base.Init();
            Log(LogType.Log, "Compile all dependencies of a given object or an entire folder.");
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
                Log(LogType.Error, "Choose an object to proceed.");
                return null;
            }

            Log(LogType.Warning, "Scanning for dependencies...");

            var assets = FolderOrFile(obj).Select(SearchHelperService.FindDependencies).ToList();
            UpdateAssets(assets, forceUpdate: true);

            Log(LogType.Warning, "Scanning ready."); 
            return assets;
        }
    }
}
