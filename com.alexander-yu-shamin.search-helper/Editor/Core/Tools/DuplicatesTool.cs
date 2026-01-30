using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor.Tools
{
    public class DuplicatesTool : ToolBase
    {
        protected override bool AreShowingFoldersSupported { get; set; } = false;
        protected override bool ShowSize { get; set; } = true;

        private Object SelectedObject { get; set; }
        private List<Asset> Assets { get; set; }
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.DuplicatesTool;

        public override void AssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            Run();
        }

        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() =>
            {
                SelectedObject = DrawObject(SelectedObject);
                EGuiKit.Button("Find", Run);

                EGuiKit.Space();
                EGuiKit.Color(Color.gray, () =>
                {
                    EGuiKit.Label("Find object duplicates or scan the selected folder for all duplicates (defaults to Assets).");
                });

                EGuiKit.FlexibleSpace();
                EGuiKit.Button(!Assets.IsNullOrEmpty() && Assets.Count == 1, "Open in Merge Tool", () =>
                {
                    TransferTo(CurrentToolType, SearchHelperWindow.ToolType.MergeTool, Assets.FirstOrDefault());
                });
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
            Assets = FindDuplicates(SelectedObject);
        }

        public override void GetDataFromAnotherTool(SearchHelperWindow.ToolType from, SearchHelperWindow.ToolType to, Asset asset)
        {
            Run(asset.Object);
        }

        private List<Asset> FindDuplicates(Object obj)
        {
            var searchedPath = string.Empty;

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
                paths = SearchHelperService.FindAssetPaths();
            }

            if (!paths.Any())
            {
                return null;
            }

            var md5 = MD5.Create();
            var dict = new Dictionary<string, List<string>>();

            var searchedHash = string.Empty;
            if (!string.IsNullOrEmpty(searchedPath))
            {
                searchedHash = SearchHelperService.GetFileHashMD5(ref md5, searchedPath);
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
                    var hash = SearchHelperService.GetFileHashMD5(ref md5, path);
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

            UpdateAssets(assets);
            return assets;
        }
    }
}
