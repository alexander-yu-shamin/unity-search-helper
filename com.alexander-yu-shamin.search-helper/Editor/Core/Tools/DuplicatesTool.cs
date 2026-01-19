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
        protected override bool DrawObjectWithEmptyDependencies { get; set; } = true;
        protected override bool IsShowFoldersSupported { get; set; } = false;
        protected override bool ShouldMainObjectsBeSorted { get; set; } = true;

        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }

        private List<ObjectContext> Contexts { get; set; }

        protected override IEnumerable<ObjectContext> Data => Contexts;

        public override void AssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            Contexts = FindDuplicates(SelectedObject);
        }

        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() =>
            {
                SelectedObject = EditorGUILayout.ObjectField(SelectedObject, typeof(Object), true,
                    GUILayout.Width(SelectedObjectWidth));
                if (UsedObject != SelectedObject)
                {
                    UsedObject = null;
                }

                EGuiKit.Button("Find", () => { Contexts = FindDuplicates(SelectedObject); });

                EGuiKit.Space();
                EGuiKit.Color(Color.gray, () =>
                {
                    EGuiKit.Label("Find object duplicates or scan the selected folder for all duplicates (defaults to Assets).");
                });

                EGuiKit.FlexibleSpace();
                EGuiKit.Button(UsedObject != null && !Contexts.IsNullOrEmpty() && Contexts.Count == 1, "Open in Merge Tool", () =>
                {
                    TransferToMergeTool(Contexts.FirstOrDefault());
                });
                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts));
        }

        public override void Run(Object selectedObject)
        {
            SelectedObject = selectedObject;
            Contexts = FindDuplicates(SelectedObject);
        }

        private List<ObjectContext> FindDuplicates(Object obj)
        {
            var searchedPath = string.Empty;

            IEnumerable<string> paths;
            if (obj != null)
            {
                UsedObject = obj;
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
                UsedObject = AssetDatabase.LoadMainAssetAtPath("Assets");
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

            Contexts = 
                dict.Where(kv => (string.IsNullOrEmpty(searchedHash) || kv.Key == searchedHash) && kv.Value.Count > 1).Select(kv =>
                {
                    var ctx = ObjectContext.FromPath(kv.Value.First());
                    ctx.Dependencies = kv.Value.Select(ObjectContext.FromPath).ToList();
                    return ctx;
                }).ToList();

            UpdateData(Contexts);
            return Contexts;
        }

        protected override void AddContextMenu(GenericMenu menu, ObjectContext context)
        {
            if (context.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            menu.AddItem(new GUIContent("Open in Merge Tool"), false, () =>
            {
                TransferToMergeTool(context);
            });
        }

        private void TransferToMergeTool(ObjectContext context)
        {
            if (context == null)
            {
                return;
            }

            if (!context.ShouldBeShown)
            {
                return;
            }

            var transferContext = new ObjectContext(context)
            {
                Dependencies = context.Dependencies.Where(dependency => dependency.ShouldBeShown).ToList()
            };

            SearchHelperWindow.TransferToTool(SearchHelperWindow.ToolType.MergeTool, transferContext);
        }
    }
}
