using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor.Tools
{
    public class DuplicatesTool : ToolBase
    {
        public override bool DrawObjectWithEmptyDependencies { get; set; } = true;
        public override bool IsShowFoldersSupported { get; set; } = false;
        public override bool IsShowEditorBuiltInSupported { get; set; } = false;

        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }

        private List<ObjectContext> Contexts { get; set; }

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
                EGuiKit.Button(UsedObject != null && !Contexts.IsNullOrEmpty(), "Open in Merge Tool", () =>
                {
                    SearchHelperWindow.TransferToTool(SearchHelperWindow.ToolType.MergeTool, Contexts);
                });
                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts));
        }

        public override void Run(Object selectedObject, Settings settings)
        {
            SelectedObject = selectedObject;
            Contexts = FindDuplicates(SelectedObject);
        }

        protected override bool Sort(SortVariant sortVariant)
        {
            if (Contexts.IsNullOrEmpty())
            {
                return false;
            }

            if (sortVariant == SortVariant.None)
            {
                return true;
            }

            foreach (var context in Contexts.Where(context => !context.Dependencies.IsNullOrEmpty()))
            {
                context.Dependencies = Sort(context.Dependencies, sortVariant).ToList();
            }

            return true;
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
                searchedHash = Hash(ref md5, searchedPath);
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
                    var hash = Hash(ref md5, path);
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

            Sort(CurrentSortVariant);
            return Contexts;
        }

        private static string Hash(ref MD5 md5, string path)
        {
            var hashBytes = md5.ComputeHash(File.ReadAllBytes(path));
            var hash = BitConverter.ToString(hashBytes);
            return hash;
        }
    }
}
