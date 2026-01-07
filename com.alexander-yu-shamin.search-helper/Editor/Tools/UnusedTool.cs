using System.Collections.Generic;
using System.Linq;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class UnusedTool : ToolBase
    {
        public override bool DrawObjectWithEmptyDependencies { get; set; } = true;
        public override bool IsShowFoldersSupported { get; set; } = false;
        public override bool IsShowEditorBuiltInSupported { get; set; } = false;

        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }
        private bool ShowAll { get; set; } = false;

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

                EGuiKit.Button("Find", () => { Contexts = FindUnused(SelectedObject); });

                EGuiKit.Space();
                EGuiKit.Color(Color.gray, () =>
                {
                    EGuiKit.Label("Similar to 'Used By', but scans all files within a folder instead of searching for dependencies on the folder itself.");
                });

                EGuiKit.FlexibleSpace();

                var newValue = EditorGUILayout.ToggleLeft("Show All", ShowAll, GUILayout.Width(100));
                if (ShowAll != newValue)
                {
                    ShowAll = newValue;
                    Contexts = FindUnused(SelectedObject);
                }

                EGuiKit.Space(HorizontalIndent);

                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts));
        }

        public override void Run(Object selectedObject)
        {
            SelectedObject = selectedObject;
            Contexts = FindUnused(SelectedObject);
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

        private List<ObjectContext> FindUnused(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            UsedObject = obj;

            var map = FolderOrFile(UsedObject).Select(ObjectContext.ToObjectContext).ToDictionary(key => key.Path);

            var paths = SearchHelperService.FindAssetPaths();
            if (!paths.Any())
            {
                return null;
            }

            foreach (var path in paths)
            {
                if (map.ContainsKey(path))
                {
                    continue;
                }

                var dependencies = AssetDatabase.GetDependencies(path);
                foreach (var dependency in dependencies)
                {
                    if (map.ContainsKey(dependency))
                    {
                        map[dependency].Dependencies.Add(ObjectContext.FromPath(path));
                    }
                }
            }

            Contexts = ShowAll ? map.Values.OrderBy(ctx => ctx.Dependencies.Count).ToList() : map.Values.Where(ctx => ctx.Dependencies.Count == 0).ToList();
            Sort(Contexts, CurrentSortVariant);
            return Contexts;
        }
    }
}
