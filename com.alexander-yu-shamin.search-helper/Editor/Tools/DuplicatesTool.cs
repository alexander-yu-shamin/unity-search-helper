using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PlasticGui.WorkspaceWindow;
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

                EGuiKit.FlexibleSpace();
                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts));
        }

        public override void Run(Object selectedObject)
        {
            if (selectedObject == null)
            {
                Debug.LogError($"Selected Object is null!");
                return;
            }

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
            IEnumerable<string> paths;
            if (obj == null)
            {
                paths = SearchHelperService.FindAssetPaths();
                if (!paths.Any())
                {
                    return null;
                }
            }
            else
            {
                UsedObject = obj;
                paths = FolderOrFile(UsedObject).Select(AssetDatabase.GetAssetPath);

                if (paths.Count() <= 1)
                {
                    return null;
                }
            }

            var md5 = MD5.Create();
            var dict = new Dictionary<string, List<string>>();

            foreach (var path in paths)
            {
                try
                {
                    var hashBytes = md5.ComputeHash(File.ReadAllBytes(path));
                    var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
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

            Contexts = dict.Where(kv => kv.Value.Count > 1).Select(kv =>
            {
                var ctx = ObjectContext.FromPath(kv.Value.First());
                ctx.Dependencies = kv.Value.Select(ObjectContext.FromPath).ToList();
                return ctx;
            }).ToList();

            Sort(CurrentSortVariant);
            return Contexts;
        }
    }
}
