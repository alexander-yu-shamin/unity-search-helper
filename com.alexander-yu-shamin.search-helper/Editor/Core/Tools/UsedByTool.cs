using System.Collections.Generic;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using UnityEngine;

namespace SearchHelper.Editor.Core.Tools
{
    public class UsedByTool : ToolBase
    {
        private Object SelectedObject { get; set; }
        private List<Asset> Assets { get; set; }
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.UsedByTool;

        public override void Init()
        {
            base.Init();
            Log(LogType.Log, "Build a dependency map and track object references.");
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
            Assets = FindUsedBy(SelectedObject);
        }

        public override void GetDataFromAnotherTool(SearchHelperWindow.ToolType from, SearchHelperWindow.ToolType to, Asset asset)
        {
            Run(asset.Object);
        }

        private List<Asset> FindUsedBy(Object obj)
        {
            if (obj == null)
            {
                Log(LogType.Error, "Choose an object to proceed.");
                return null;
            }

            Log(LogType.Warning, "Scanning for dependants...");

            var searchedCtx = SearchHelperService.FindUsedBy(obj, IsCacheUsed);

            if (searchedCtx.IsFolder)
            {
                ShowFolders = true;
            }

            var assets = new List<Asset>() { searchedCtx };
            UpdateAssets(assets, forceUpdate: true);

            Log(LogType.Warning, "Scanning ready."); 
            return assets;
        }
    }
}
