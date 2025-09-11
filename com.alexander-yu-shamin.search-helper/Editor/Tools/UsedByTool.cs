using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class UsedByTool : ToolBase
    {
        public override string Name { get; set; } = SearchHelperSettings.UsedByToolName;
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

                EGuiKit.Button(SearchHelperSettings.UsesToolButtonText, () =>
                {
                    Contexts = FindUsedBy(SelectedObject);
                });

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
            Contexts = FindUsedBy(SelectedObject);
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

        private List<ObjectContext> FindUsedBy(Object obj)
        {
            var result = new List<ObjectContext>();

            if (obj == null)
            {
                return result;
            }

            UsedObject = obj;
            var searchedCtx = ObjectContext.ToObjectContext(obj);

            var paths = SearchHelperService.FindAssetPaths();
            if (!paths.Any())
            {
                return result;
            }

            foreach (var path in paths)
            {
                if (path == searchedCtx.Path)
                {
                    continue;
                }

                var dependencies = AssetDatabase.GetDependencies(path);
                foreach (var dependency in dependencies)
                {
                    if (dependency == searchedCtx.Path)
                    {
                        searchedCtx.Dependencies.Add(ObjectContext.FromPath(path));
                        break;
                    }
                }
            }

            result.Add(searchedCtx);
            return result;
        }
    }
}
