using System.Collections.Generic;
using System.Linq;
using SearchHelper.Editor.Core;
using Toolkit.Editor.Helpers.IMGUI;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class UsedByTool : ToolBase
    {
        protected override bool IsCacheUsed { get; set; } = false;
        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }

        private List<ObjectContext> Contexts { get; set; }
        protected override IEnumerable<ObjectContext> Data => Contexts;
        protected override string EmptyObjectContextText
        {
            get
            {
                if (IsScopeRulesSupported)
                {
                    if (IsGlobalScope)
                    {
                        return "This object is not referenced anywhere in the project.";
                    }
                    else
                    {
                        return "This object is not referenced locally";
                    }
                }
                else
                {
                    return "This object is not referenced anywhere in the project.";
                }
            }
        } 


        private Model DrawModel { get; set; }
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

                EGuiKit.Button("Find", Run);
                EGuiKit.FlexibleSpace();
                DrawHeaderControls();
            });

            DrawModel ??= new Model()
            {
                DrawObjectWithEmptyDependencies = true
            };

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts, DrawModel));
        }

        public override void Run(Object selectedObject)
        {
            if (selectedObject == null)
            {
                Debug.LogError($"Selected Object is null!");
                return;
            }

            SelectedObject = selectedObject;
            Run();
        }

        public override void Run()
        {
            Contexts = FindUsedBy(SelectedObject);
        }

        private List<ObjectContext> FindUsedBy(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            UsedObject = obj;
            var searchedCtx = SearchHelperService.FindUsedBy(obj, IsCacheUsed);

            if (searchedCtx.IsFolder)
            {
                IsFoldersShown = true;
            }

            var contexts = new List<ObjectContext>() { searchedCtx };
            UpdateData(contexts);
            return contexts;
        }
    }
}
