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
        protected override bool AreScopeRulesSupported { get; set; } = true;
        protected override bool ShowSize { get; set; } = true;

        protected override string EmptyObjectContextText
        {
            get
            {
                if (AreScopeRulesSupported)
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

                EGuiKit.Space();
                EGuiKit.Color(Color.gray, () =>
                {
                    EGuiKit.Label("Similar to 'Used By', but scans all files within the folder");
                });

                EGuiKit.FlexibleSpace();
                DrawHeaderControls();
            });

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, _assets));
        }

        public override void Run(Object selectedObject)
        {
            SelectedObject = selectedObject;
            Run();
        }

        public override void Run()
        {
            _assets = FindUnused(SelectedObject);
        }

        protected override void AddSettingsContextMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Show Unused Items"), ShowUnusedItems, () =>
            {
                ShowUnusedItems = !ShowUnusedItems;
                UpdateAssets(_assets);
            });

            menu.AddItem(new GUIContent("Show Used Items"), ShowUsedItems, () =>
            {
                ShowUsedItems = !ShowUsedItems;
                UpdateAssets(_assets);
            });
        }

        protected override void AddActionContextMenu(GenericMenu menu)
        {
            if (_assets.IsNullOrEmpty())
            {
                menu.AddDisabledItem(new GUIContent("Remove Unused Items"));
            }
            else
            {
                menu.AddItem(new GUIContent("Remove Unused Items"), false, RemovedUnusedItems);
            }
        }

        private List<Asset> FindUnused(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            UsedObject = obj;

            var map = FolderOrFile(UsedObject).Select(Asset.ToObjectContext).ToDictionary(key => key.Path);

            var root = IsGlobalScope ? null : FolderPathFromObject(obj);
            var dependencyMap = SearchHelperService.BuildDependencyMap(root, IsCacheUsed);
            foreach (var (path, context) in map)
            {
                if (dependencyMap.TryGetValue(path, out var dependencies))
                {
                    context.Dependencies = dependencies.ToList();
                }
            }

            var contexts = map.Values.ToList();
            UpdateAssets(contexts);
            return contexts;
        }

        //protected override bool ShouldBeShown(ObjectContext objectContext, ObjectContext parentContext = null)
        //{
        //    var isTopLevel = parentContext == null;

        //    if (!isTopLevel)
        //    {
        //        if (!ShowUsedItems)
        //        {
        //            return false;
        //        }
        //    }
        //    else
        //    {
        //        var hasDependencies = objectContext.Dependencies.Count != 0;
        //        if (hasDependencies && !ShowUsedItems)
        //        {
        //            return false;
        //        }

        //        if (!hasDependencies && !ShowUnusedItems)
        //        {
        //            return false;
        //        }
        //    }

        //    return base.ShouldBeShown(objectContext, parentContext);
        //}

        private void RemovedUnusedItems()
        {
            if (_assets.IsNullOrEmpty())
            {
                return;
            }

            AssetDatabase.StartAssetEditing();
            foreach (var asset in _assets)
            {
                if (!IsMainAssetVisible(asset))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(asset.Path))
                {
                    continue;
                }

                File.Delete(asset.Path);
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
    }
}
