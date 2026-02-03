using System.Collections.Generic;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Core.Tools
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
        protected override IEnumerable<Asset> Data => Contexts;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.FindByGuid;

        public override void InnerDraw(Rect windowRect)
        {
            var height = GUILayout.Height(UISettings.AssetHeaderHeight);
            var width = GUILayout.Width(UISettings.CommonGuidTextWidth);
            if (IsFullScreenMode)
            {
                HeaderLineCount = 1;
                EGuiKit.Horizontal(() =>
                {
                    DrawFindGuidField(height);
                    EGuiKit.FlexibleSpace();
                    DrawSelectedObjectGuidField(height);
                });
            }
            else
            {
                HeaderLineCount = 2;
                EGuiKit.Vertical(() =>
                {
                    EGuiKit.Horizontal(() =>
                    {
                        DrawFindGuidField(height);
                        EGuiKit.FlexibleSpace();
                    });

                    EGuiKit.Horizontal(() =>
                    {
                        DrawSelectedObjectGuidField(height, UISettings.CommonGuidTextWidth * 2 + 4, false);
                        EGuiKit.FlexibleSpace();
                    });
                });
            }

            if (Contexts.IsNullOrEmpty() && !string.IsNullOrEmpty(CurrentGuid))
            {
                EGuiKit.Horizontal(
                    () =>
                    {
                        EGuiKit.Color(UISettings.ErrorColor,
                            () => { EGuiKit.Label($"Object referenced by GUID {CurrentGuid} could not be located."); });
                    }, GUI.skin.box);
            }

            if (!Contexts.IsNullOrEmpty())
            {
                DrawVirtualScroll(Contexts);
            }

            DrawLogView();
        }

        private void DrawSelectedObjectGuidField(GUILayoutOption height, float selectedObjectWidth = 0.0f, bool drawLabel = true)
        {
            if (selectedObjectWidth == 0.0f)
            {
                SelectedObject = EGuiKit.Object(SelectedObject, typeof(Object), true, null, height);
            }
            else
            {
                SelectedObject = EGuiKit.Object(SelectedObject, typeof(Object), true, null, height,
                    GUILayout.Width(selectedObjectWidth));
            }

            if (drawLabel)
            {
                EGuiKit.Label("=>");
            }

            if (UsedObject != SelectedObject)
            {
                UsedObject = SelectedObject;
                CurrentUsedObjectGuid = string.Empty;
                CurrentUsedObjectGuid = SearchHelperService.GetObjectGuid(UsedObject);
            }

            EditorGUILayout.TextField(CurrentUsedObjectGuid, GUILayout.Width(UISettings.CommonGuidWidth),
                height);
        }

        private void DrawFindGuidField(GUILayoutOption height)
        {
            var width = GUILayout.Width(UISettings.CommonGuidTextWidth);
            EGuiKit.Label("GUID:", height, width);
            var newGuid = EditorGUILayout.TextField(CurrentGuid,
                GUILayout.Width(UISettings.CommonGuidWidth), height);
            if (newGuid != CurrentGuid)
            {
                CurrentGuid = newGuid;
                FindAssetByGuid(CurrentGuid);
            }

            EGuiKit.Button("Find", () => { FindAssetByGuid(CurrentGuid); }, width);
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
            var foundObject = SearchHelperService.FindObjectByGuid(guid);
            if (foundObject == null)
            {
                Log(LogType.Error, $"Object referenced by GUID {guid} could not be located.");
                Contexts = null;
                return;
            }

            Log(LogType.Warning, $"Object referenced by GUID {guid} has been found.");
            Contexts = Asset.ToAsset(foundObject).AsList();
        }
    }
}