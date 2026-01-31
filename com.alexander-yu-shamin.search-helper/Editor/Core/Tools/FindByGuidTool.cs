using System.Collections.Generic;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class FindByGuidTool : ToolBase
    {
        protected override bool ShowEmptyDependencyText { get; set; } = false;
        protected override bool ShowDependenciesCount { get; set; } = false;

        private string CurrentGuid { get; set; } 
        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }
        private string CurrentUsedObjectGuid { get; set; }
        private List<Asset> Contexts { get; set; }

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.FindByGuidTool;
        protected override IEnumerable<Asset> Data => Contexts;
        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() =>
            {
                var height = GUILayout.Height(HeaderHeight);

                EGuiKit.Label("GUID:", height);
                var newGuid = EditorGUILayout.TextField(CurrentGuid, GUILayout.Width(GuidTextAreaWidth), height);
                if (newGuid != CurrentGuid)
                {
                    CurrentGuid = newGuid;
                    FindAssetByGuid(CurrentGuid);
                }

                EGuiKit.Button("Find", () =>
                {
                    FindAssetByGuid(CurrentGuid);
                }, height);

                EGuiKit.FlexibleSpace();

                SelectedObject = DrawObject(SelectedObject);

                if (UsedObject != SelectedObject)
                {
                    UsedObject = SelectedObject;
                    CurrentUsedObjectGuid = string.Empty;
                    CurrentUsedObjectGuid = SearchHelperService.GetObjectGuid(UsedObject);
                }
                
                EditorGUILayout.TextField(CurrentUsedObjectGuid, GUILayout.Width(GuidTextAreaWidth), height);
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
            Run();
        }

        public override void Run()
        {
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

            Contexts = Asset.ToAsset(foundObject).AsList();
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

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts));
        }
    }
}
