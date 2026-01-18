using System.Collections.Generic;
using System.Linq;
using SearchHelper.Editor.Core;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class DependenciesTool : ToolBase
    {
        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }
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
                    FindDependencies(SelectedObject);
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
            FindDependencies(SelectedObject);
        }

        private List<ObjectContext> FindDependencies(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            UsedObject = obj;
            Contexts = FolderOrFile(obj).Select(SearchHelperService.FindDependencies).ToList();
            UpdateData(Contexts);
            return Contexts;
        }
    }
}
