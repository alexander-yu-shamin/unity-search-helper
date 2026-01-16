using System.Collections.Generic;
using System.Linq;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class UsedByTool : ToolBase
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

                EGuiKit.Button("Find", () =>
                {
                    FindUsedBy(SelectedObject);
                });

                EGuiKit.FlexibleSpace();
                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts));
        }

        public override void Run(Object selectedObject, Settings settings)
        {
            if (selectedObject == null)
            {
                Debug.LogError($"Selected Object is null!");
                return;
            }

            SelectedObject = selectedObject;
            FindUsedBy(SelectedObject);
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
            if (obj == null)
            {
                return null;
            }

            UsedObject = obj;
            var searchedCtx = SearchHelperService.FindUsedBy(obj);

            Contexts = new List<ObjectContext>() { searchedCtx };
            Sort(CurrentSortVariant);
            return Contexts;
        }
    }
}
