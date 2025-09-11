using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Codice.Client.BaseCommands.Merge;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Color = UnityEngine.Color;

namespace SearchHelper.Editor.Tools
{
    public class DependenciesTool : ToolBase
    {
        public string Name { get; set; } = SearchHelperSettings.DependenciesToolName;

        public Object SelectedObject { get; set; }
        public Object UsedObject { get; set; }
        public List<ObjectContext> Contexts { get; set; }

        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() =>
            {
                SelectedObject = EditorGUILayout.ObjectField(SelectedObject, typeof(Object), true);
                if (UsedObject != SelectedObject)
                {
                    UsedObject = null;
                }

                EGuiKit.Button(SearchHelperSettings.UsesToolButtonText, () =>
                {
                    FindDependencies(SelectedObject);
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
            FindDependencies(SelectedObject);
        }

        private List<ObjectContext> FindDependencies(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            UsedObject = obj;

            var path = AssetDatabase.GetAssetPath(UsedObject);
            if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
            {
                Contexts = new List<ObjectContext> { SearchHelperService.FindDependencies(obj) };
            }
            else
            {
                var objectsInFolder = SearchHelperService.FindAssets(path);
                Contexts = objectsInFolder.Select(SearchHelperService.FindDependencies).ToList();
            }

            return Contexts;
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

            foreach (var context in Contexts)
            {
                if (context.Dependencies.IsNullOrEmpty())
                {
                    continue;
                }

                context.Dependencies = Sort(context.Dependencies, sortVariant).ToList();
            }

            return true;
        }

    }
}
