using System.Collections.Generic;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class UsedByTool : ToolBase
    {
        protected override bool IsCacheUsed { get; set; } = false;
        private Object SelectedObject { get; set; }
        private List<Asset> Assets { get; set; }
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.UsedByTool;

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
                return null;
            }

            var searchedCtx = SearchHelperService.FindUsedBy(obj, IsCacheUsed);

            if (searchedCtx.IsFolder)
            {
                ShowFolders = true;
            }

            var assets = new List<Asset>() { searchedCtx };
            UpdateAssets(assets);
            return assets;
        }
    }
}
