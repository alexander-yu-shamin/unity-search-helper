using System.Collections.Generic;
using System.Linq;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor.Core.Tools
{
    public class DuplicatesTool : ToolBase
    {
        protected override bool AreShowingFoldersSupported { get; set; } = false;
        protected override bool IsMetaDiffSupported { get; set; } = true;
        protected override bool ShowSize { get; set; } = true;

        private Object SelectedObject { get; set; }
        private List<Asset> Assets { get; set; }
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.DuplicatesTool;

        public override void AssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
        }

        public override void Init()
        {
            base.Init();
            DefaultDrawModel.DrawState = true;
            DefaultDrawModel.GetSizeTooltipText = GetFullSize;
            Log(LogType.Log, $"Find object duplicates or scan the selected folder for all duplicates (defaults to Assets).");
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
            }, secondLineLeft: () =>
            {
                EGuiKit.Button(!Assets.IsNullOrEmpty() && Assets.Count == 1, "Open in Merge Tool", () =>
                {
                    TransferTo(CurrentToolType, SearchHelperWindow.ToolType.MergeTool, Assets.FirstOrDefault());
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
            Assets = FindDuplicates(SelectedObject);
        }

        public override void GetDataFromAnotherTool(SearchHelperWindow.ToolType from, SearchHelperWindow.ToolType to, Asset asset)
        {
            Run(asset.Object);
        }

        private List<Asset> FindDuplicates(Object obj)
        {
            var searchedPath = string.Empty;

            Log(LogType.Warning, "Scanning for duplicates...");
            IEnumerable<string> paths;
            if (obj != null)
            {
                var objPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(objPath) && AssetDatabase.IsValidFolder(objPath))
                {
                    paths = SearchHelperService.FindAssetPaths(objPath);
                }
                else
                {
                    searchedPath = objPath;
                    paths = SearchHelperService.FindAssetPaths();
                }
            }
            else
            {
                var selectedObj = AssetDatabase.LoadMainAssetAtPath("Assets");
                if (selectedObj != null)
                {
                    SelectedObject = selectedObj;
                }

                paths = SearchHelperService.FindAssetPaths();
            }

            if (!paths.Any())
            {
                return null;
            }

            var dict = new Dictionary<string, List<string>>();

            var searchedHash = string.Empty;
            if (!string.IsNullOrEmpty(searchedPath))
            {
                searchedHash = DiffManager.GetFileHashMd5(searchedPath);
                if (string.IsNullOrEmpty(searchedHash))
                {
                    Debug.Log($"Can't count Hash for {searchedPath}");
                    return null;
                }
            }

            foreach (var path in paths)
            {
                try
                {
                    var hash = DiffManager.GetFileHashMd5(path);
                    if (dict.ContainsKey(hash))
                    {
                        dict[hash].Add(path);
                    }
                    else
                    {
                        dict.Add(hash, new List<string>() { path });
                    }
                }
                catch
                {
                    // ignored
                }
            }

            var assets = 
                dict.Where(kv => (string.IsNullOrEmpty(searchedHash) || kv.Key == searchedHash) && kv.Value.Count > 1).Select(kv =>
                {
                    var ctx = Asset.FromPath(kv.Value.First());
                    ctx.Dependencies = kv.Value.Select(Asset.FromPath).ToList();
                    return ctx;
                }).ToList();

            UpdateAssets(assets, forceUpdate: true);
            Log(LogType.Warning, "Scanning ready.");
            return assets;
        }

        private string GetFullSize(Asset asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var size = asset.Size;
            var dependencyCount = asset.Dependencies?.Count ?? 1;
            return FormatExtensions.ToHumanReadableSize(size * dependencyCount);
        }
    }
}
