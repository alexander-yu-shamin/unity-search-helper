using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SearchHelper.Editor.Core;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class UnusedTool : ToolBase
    {
        protected override bool DrawObjectWithEmptyDependencies { get; set; } = true;
        protected override bool IsShowFoldersSupported { get; set; } = false;
        protected override bool ShouldMainObjectsBeSorted { get; set; } = true;
        protected override bool IsScopeRulesSupported { get; set; } = true;

        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }
        private bool ShowAll { get; set; } = false;

        private List<ObjectContext> Contexts { get; set; }

        protected override IEnumerable<ObjectContext> Data => Contexts;

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

                EGuiKit.Button("Find", () =>
                {
                    Contexts = FindUnused(SelectedObject);
                });

                EGuiKit.Space();
                EGuiKit.Color(Color.gray, () =>
                {
                    EGuiKit.Label("Similar to 'Used By', but scans all files within the folder");
                });

                EGuiKit.FlexibleSpace();

                var newValue = EditorGUILayout.ToggleLeft("Show All", ShowAll, GUILayout.Width(70));
                if (ShowAll != newValue)
                {
                    ShowAll = newValue;
                    Contexts = FindUnused(SelectedObject);
                }

                EGuiKit.Button(!Contexts.IsNullOrEmpty(), "Remove Items", RemovedUnusedItems);
                EGuiKit.Button(SelectedObject != null && !Contexts.IsNullOrEmpty(), "Copy Items to Clipboard", CopyToClipboard);

                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts));
        }

        public override void Run(Object selectedObject)
        {
            SelectedObject = selectedObject;
            Contexts = FindUnused(SelectedObject);
        }

        private List<ObjectContext> FindUnused(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            UsedObject = obj;

            var map = FolderOrFile(UsedObject).Select(ObjectContext.ToObjectContext).ToDictionary(key => key.Path);

            var root = IsGlobalScope ? null : FolderPathFromObject(obj);

            var paths = SearchHelperService.FindAssetPaths(root);
            if (!paths.Any())
            {
                return null;
            }

            foreach (var path in paths)
            {
                var dependencies = AssetDatabase.GetDependencies(path);
                foreach (var dependency in dependencies)
                {
                    if (map.ContainsKey(dependency))
                    {
                        map[dependency].Dependencies.Add(ObjectContext.FromPath(path));
                    }
                }
            }

            foreach (var element in map.Values)
            {
                element.Dependencies = element.Dependencies.Where(dependency => dependency.Guid != element.Guid).ToList();
            }

            Contexts = ShowAll ? map.Values.OrderBy(ctx => ctx.Dependencies.Count).ToList() : map.Values.Where(ctx => ctx.Dependencies.Count == 0).ToList();
            UpdateData(Contexts);
            return Contexts;
        }

        private void RemovedUnusedItems()
        {
            if (Contexts.IsNullOrEmpty())
            {
                return;
            }

            AssetDatabase.StartAssetEditing();
            foreach (var context in Contexts)
            {
                if (!context.ShouldBeShown)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(context.Path))
                {
                    continue;
                }

                File.Delete(context.Path);
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        private void CopyToClipboard()
        {
            if (Contexts.IsNullOrEmpty())
            {
                return;
            }

            CopyToClipboard(string.Join("\n", Contexts.Where(context => context.ShouldBeShown).Select(context => context.Path)));
        }
    }
}
