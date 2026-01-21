using System.Collections.Generic;
using System.IO;
using System.Linq;
using SearchHelper.Editor.Core;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Tools
{
    public class UnusedTool : ToolBase
    {
        protected override bool DrawObjectWithEmptyDependencies { get; set; } = true;
        protected override bool ShouldMainObjectsBeSorted { get; set; } = true;
        protected override bool IsScopeRulesSupported { get; set; } = true;
        protected override bool IsSizeShowingSupported { get; set; } = true;

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

        private Object SelectedObject { get; set; }
        private Object UsedObject { get; set; }
        private bool ShowUsedItems { get; set; } = false;
        private bool ShowUnusedItems { get; set; } = true;

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

                EGuiKit.Button("Find", Run);

                EGuiKit.Space();
                EGuiKit.Color(Color.gray, () =>
                {
                    EGuiKit.Label("Similar to 'Used By', but scans all files within the folder");
                });

                EGuiKit.FlexibleSpace();
                EGuiKit.Button(!Contexts.IsNullOrEmpty(), "Remove Items", RemovedUnusedItems);
                EGuiKit.Button(SelectedObject != null && !Contexts.IsNullOrEmpty(), "Copy Items to Clipboard", CopyToClipboard);

                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts));
        }

        public override void Run(Object selectedObject)
        {
            SelectedObject = selectedObject;
            Run();
        }

        public override void Run()
        {
            Contexts = FindUnused(SelectedObject);
        }

        protected override void AddSettingsContextMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Show Unused Items"), ShowUnusedItems, () =>
            {
                ShowUnusedItems = !ShowUnusedItems;
                UpdateData(Contexts);
            });

            menu.AddItem(new GUIContent("Show Used Items"), ShowUsedItems, () =>
            {
                ShowUsedItems = !ShowUsedItems;
                UpdateData(Contexts);
            });
        }

        private List<ObjectContext> FindUnused(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            UsedObject = obj;

            var map = FolderOrFile(UsedObject).Select(ObjectContext.ToObjectContext).ToDictionary(key => key.Path);

            var root = IsGlobalScope ? null : FolderPathFromObject(obj);
            var dependencyMap = SearchHelperService.BuildDependencyMap(root, IsCacheUsed);
            foreach (var (path, context) in map)
            {
                if (dependencyMap.TryGetValue(path, out var dependencies))
                {
                    context.Dependencies = dependencies.Where(dependency => dependency.Guid != context.Guid).ToList();
                }
            }

            var contexts = map.Values.ToList();
            UpdateData(contexts);
            return contexts;
        }

        protected override bool ShouldBeShown(ObjectContext objectContext, ObjectContext parentContext = null)
        {
            var isTopLevel = parentContext == null;

            if (!isTopLevel)
            {
                if (!ShowUsedItems)
                {
                    return false;
                }
            }
            else
            {
                var hasDependencies = objectContext.Dependencies.Count != 0;
                if (hasDependencies && !ShowUsedItems)
                {
                    return false;
                }

                if (!hasDependencies && !ShowUnusedItems)
                {
                    return false;
                }
            }

            return base.ShouldBeShown(objectContext, parentContext);
        }

        private void RemovedUnusedItems()
        {
            if (Contexts.IsNullOrEmpty())
            {
                return;
            }

            AssetDatabase.StartAssetEditing();
            foreach (var context in Contexts)
            {
                if (!context.ShouldBeShown)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(context.Path))
                {
                    continue;
                }

                File.Delete(context.Path);
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private void CopyToClipboard()
        {
            if (Contexts.IsNullOrEmpty())
            {
                return;
            }

            CopyToClipboard(string.Join("\n", Contexts.Where(context => context.ShouldBeShown).Select(context => context.Path)));
        }
    }
}
