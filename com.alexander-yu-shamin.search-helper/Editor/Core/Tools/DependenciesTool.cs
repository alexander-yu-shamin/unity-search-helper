using System.Collections.Generic;
using System.Linq;
using SearchHelper.Editor.Core;
using Toolkit.Editor.Helpers.IMGUI;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class DependenciesTool : ToolBase
    {
        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }

        private List<Asset> _assets { get; set; }
        protected override IEnumerable<Asset> Assets => _assets;

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

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, _assets));
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
            _assets = FindDependencies(SelectedObject);
        }

        private List<Asset> FindDependencies(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            UsedObject = obj;
            var assets = FolderOrFile(obj).Select(SearchHelperService.FindDependencies).ToList();
            UpdateAssets(assets);
            return assets;
        }
    }
}
