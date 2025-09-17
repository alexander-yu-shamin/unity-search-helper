using System.Collections.Generic;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class FindByGuidTool : ToolBase
    {
        public override string Name { get; set; } = SearchHelperSettings.FindByGuidToolName;
        private string CurrentGuid { get; set; } 
        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }
        private string CurrentUsedObjectGuid { get; set; }

        private List<ObjectContext> Contexts { get; set; }

        public override bool DrawObjectWithEmptyDependencies { get; set; } = true;

        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() => 
            {
                EGuiKit.Label("GUID:");
                var newGuid = EditorGUILayout.TextField(CurrentGuid, GUILayout.Width(GuidTextAreaWidth));
                if (newGuid != CurrentGuid)
                {
                    CurrentGuid = newGuid;
                    FindAssetByGuid(CurrentGuid);
                }

                EGuiKit.Space(HorizontalIndent);
                EGuiKit.Button("Find", () =>
                {
                    FindAssetByGuid(CurrentGuid);
                });

                EGuiKit.FlexibleSpace();

                SelectedObject = EditorGUILayout.ObjectField(SelectedObject, typeof(Object), true,
                    GUILayout.Width(SelectedObjectWidth));

                if (UsedObject != SelectedObject)
                {
                    UsedObject = SelectedObject;
                    CurrentUsedObjectGuid = string.Empty;
                    CurrentUsedObjectGuid = SearchHelperService.GetObjectGuid(UsedObject);
                }
                
                EditorGUILayout.TextField(CurrentUsedObjectGuid, GUILayout.Width(GuidTextAreaWidth));
            });

            DrawContexts(windowRect);
        }

        public override void Run(Object selectedObject)
        {
            if (selectedObject == null)
            {
                return;
            }

            SelectedObject = UsedObject = selectedObject;
            CurrentUsedObjectGuid = string.Empty;
            CurrentUsedObjectGuid = SearchHelperService.GetObjectGuid(UsedObject);
        }

        private void FindAssetByGuid(string guid)
        {
            var foundObject = SearchHelperService.FindObjectByGuid(CurrentGuid);
            if (foundObject == null)
            {
                Contexts = null;
                return;
            }

            Contexts = ObjectContext.ToObjectContext(foundObject).ToList();
        }

        private void DrawContexts(Rect windowRect)
        {
            if (Contexts.IsNullOrEmpty() && !string.IsNullOrEmpty(CurrentGuid))
            {
                EGuiKit.Horizontal(() =>
                {
                    EGuiKit.Color(ErrorColor, () =>
                    {
                        EGuiKit.Label($"The object with GUID {CurrentGuid} was not found.");
                    });
                }, GUI.skin.box);
                return;
            }

            if (Contexts.IsNullOrEmpty())
            {
                return;
            }

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts, false));
        }
    }
}
