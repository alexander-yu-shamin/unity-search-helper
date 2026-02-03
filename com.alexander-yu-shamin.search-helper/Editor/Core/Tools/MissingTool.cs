using System.Collections.Generic;
using System.Linq;
using Codice.Client.IssueTracker;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Core.Tools
{
    public class MissingTool : ToolBase
    {
        private Object SelectedObject { get; set; }
        private List<Asset> Assets { get; set; }
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.DependencyTool;

        public override void Init()
        {
            base.Init();
            Log(LogType.Log, "Find missing elements in objects.");
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
            Assets = FindMissingElements(SelectedObject);
        }

        protected override string GetEmptyAssetText(Asset mainAsset)
        {
            if (mainAsset.IsFolder)
            {
                return "Folders are not supported.";
            }
            else
            {
                return "The asset has missing elements.";
            }
        }

        private List<Asset> FindMissingElements(Object obj)
        {
            Log(LogType.Warning, "Scanning for missing elements...");

            if (obj == null)
            {
                obj = AssetDatabase.LoadMainAssetAtPath("Assets");
                if (obj != null)
                {
                    SelectedObject = obj;
                }
            }

            if (obj == null)
            {
                Log(LogType.Error, "Choose an object to proceed.");
                return null;
            }

            var assets = FolderOrFile(obj).Select(SearchHelperService.FindMissing).Where(asset => asset != null).ToList();
            UpdateAssets(assets, forceUpdate: true);
            Log(LogType.Warning, "Scanning ready."); 
            return assets;
        }
    }
}
