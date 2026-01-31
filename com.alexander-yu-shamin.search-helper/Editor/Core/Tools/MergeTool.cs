using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.Diagnostics;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor.Tools
{
    public class MergeTool : ToolBase
    {
        protected override bool AreShowingFoldersSupported { get; set; } = false;
        protected override bool AreFilterByRuleSupported { get; set; } = false;
        protected override bool ShowEmptyDependencyText { get; set; } = false;
        protected override bool IsMetaDiffSupported { get; set; } = true;
        protected override bool MetaDiffEnabled { get; set; } = true;

        private Asset BaseObject { get; set; }
        private Model DrawModel { get; set; }
        private bool ShowDependents { get; set; } = false;
        private List<Asset> Assets { get; set; } = new();
        protected override IEnumerable<Asset> Data => Assets;

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.MergeTool;


        public override void AssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            //using var measure = Profiler.Measure("AssetChanged");
            //foreach (var asset in Contexts)
            //{
            //    if (asset.Object != null)
            //    {
            //        asset.IsMerged = false;
            //    }
            //}

            //CompareWithBaseObject(Contexts);
        }

        public override void Init()
        {
            base.Init();

            DefaultModel.DrawMergeButtons = true;
            DefaultModel.DrawState = true;
            DefaultModel.OnSelectedButtonPressed = SelectedButtonPressedHandler;
            DefaultModel.OnRemoveButtonPressed = RemoveButtonPressedHandler;
            DefaultModel.OnComparandButtonPressed = ComparandButtonPressedHandler;
            DefaultModel.OnDiffButtonPressed = DiffButtonPressedHandler;
        }

        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() =>
            {
                EGuiKit.Button("Add Selected Object as Base", () => { AddToMerge(Selection.activeObject, isBaseAsset: true); });
                EGuiKit.Button("Add Selected Object as Theirs", () => { AddToMerge(Selection.activeObject); });

                EGuiKit.Button(BaseObject != null || !Assets.IsNullOrEmpty(), "Clear", () =>
                {
                    BaseObject = null;
                    Assets = null;
                });

                EGuiKit.FlexibleSpace();
                EGuiKit.Button(BaseObject != null && IsMainAssetVisible(BaseObject) && !Assets.IsNullOrEmpty(), "Merge", () =>
                {
                    Merge(BaseObject, Assets, IsCacheUsed);
                });

                EGuiKit.Space(HeaderIndent);
                DrawSelectButton();

                DrawHeaderControls();
            });

            EGuiKit.Space(HeaderPadding);

            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Assets, DrawModel));
        }

        public override void Run(Object selectedObject)
        {
            if (selectedObject != null)
            {
                AddToMerge(selectedObject, isBaseAsset: true);
            }
        }

        public override void Run()
        {
        }

        public override void GetDataFromAnotherTool(SearchHelperWindow.ToolType from,
            SearchHelperWindow.ToolType to, Asset asset)
        {
            using var measure = Profiler.Measure("GetDataFromAnotherTool");

            if (asset == null || asset.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            Assets ??= new List<Asset>();
            if (Assets.Any(element => element.Path == asset.Path))
            {
                Debug.LogError($"The asset {asset.Path} has already added.");
                return;
            }

            BaseObject = null;

            AddToMerge(asset.Object, isBaseAsset: true, batching: true);

            foreach (var dependency in asset.Dependencies.Where(dependency => dependency.Path != asset.Path))
            {
                AddToMerge(dependency.Object, batching: true);
            }


            UpdateDependents(ShowDependents);
            UpdateAssets(Assets, forceUpdate: true);
        }

        protected override void AddSettingsContextMenu(GenericMenu menu)
        {
            base.AddSettingsContextMenu(menu);
            menu.AddItem(new GUIContent("Show Dependents"), ShowDependents, () =>
            {
                ShowDependents = !ShowDependents;
                UpdateDependents(ShowDependents);
            });
        }

        private void DrawSelectButton()
        {
            var content = new GUIContent($"Selection");
            if (EditorGUILayout.DropdownButton(content, FocusType.Passive, GUILayout.Width(75)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Select All"), false,
                    () =>
                    {
                        Assets.ForEach(asset => asset.IsSelected = IsMainAssetVisible(asset));
                    });

                menu.AddItem(new GUIContent("Unselect All"), false,
                    () =>
                    {
                        Assets.ForEach(asset => asset.IsSelected = false);
                    });

                menu.AddItem(new GUIContent("Select Similar"), false,
                    () =>
                    {
                        Assets.ForEach(asset => asset.IsSelected = asset.MetaDiffState == AssetDiffState.SameAsBaseObject && IsMainAssetVisible(asset));
                    });

                menu.ShowAsContext();
            }
        }

        #region Merge
        private void Merge(Asset baseObject, List<Asset> contexts, bool isCacheUsed)
        {
            using var measure = Profiler.Measure("Merge");
            if (contexts.IsNullOrEmpty() || baseObject == null)
            {
                return;
            }

            AssetDatabase.StartAssetEditing();

            var dependencyMap = SearchHelperService.BuildDependencyMap(useCache: isCacheUsed);

            //foreach (var asset in contexts.Where(asset => asset is { IsBaseObject: false, IsSelected: true, IsVisible: true }))
            foreach (var asset in contexts.Where(asset => asset is { IsBaseObject: false, IsSelected: true }))
            {
                List<Asset> dependencies = null;
                if (!dependencyMap.TryGetValue(asset.Path, out dependencies))
                {
                    dependencies = SearchHelperService.FindUsedBy(asset.Object, isCacheUsed)?.Dependencies;
                }

                if(dependencies == null)
                {
                    Debug.LogError($"Can't find dependencies for {asset.Path}");
                    continue;
                }

                Merge(baseObject, asset, dependencies);

                using (Profiler.Measure($"Merge::Delete {asset.Path}"))
                {
                    File.Delete(asset.Path);
                }

                using (Profiler.Measure($"Merge::Delete {asset.MetaPath}"))
                {
                    File.Delete(asset.MetaPath);
                }
                asset.IsMerged = true;
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void Merge(Asset baseObject, Asset theirsObject, List<Asset> dependencies)
        {
            using var measure = Profiler.Measure("MergeElement");
            foreach (var dependency in dependencies)
            {
                var dependencyObject = new Asset(dependency);

                if (!string.IsNullOrEmpty(dependencyObject.Path))
                {
                    using (Profiler.Measure($"MergeElement::Merge {dependencyObject.Path}"))
                    {
                        if (File.Exists(dependencyObject.Path))
                        {
                            var text = File.ReadAllText(dependencyObject.Path);
                            var replaced = text.Replace(theirsObject.Guid, baseObject.Guid);
                            File.WriteAllText(dependencyObject.Path, replaced);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(dependencyObject.MetaPath))
                {
                    using (Profiler.Measure($"MergeElement::Merge{dependencyObject.MetaPath}"))
                    {
                        if (File.Exists(dependencyObject.MetaPath))
                        {
                            var text = File.ReadAllText(dependencyObject.MetaPath);
                            var replaced = text.Replace(theirsObject.Guid, baseObject.Guid);
                            File.WriteAllText(dependencyObject.MetaPath, replaced);
                        }
                    }
                }
            }
        }

        private void AddToMerge(Object selectedObject, bool isBaseAsset = false, bool batching = false)
        {
            if (selectedObject == null)
            {
                return;
            }

            Assets ??= new List<Asset>();

            var newAsset = Asset.ToAsset(selectedObject);
            var validationError = ValidateAsset(newAsset);

            switch (validationError)
            {
                case ValidationError.Null:
                case ValidationError.NotAFile:
                {
                    Debug.LogError($"The asset {selectedObject.name}:{newAsset.Path} can't be used");
                    return;
                }
                case ValidationError.BaseObject:
                {
                    if (BaseObject == null)
                    {
                        Debug.LogError($"The asset {newAsset.Object.name}:{newAsset.Path} is a base, but the Base is null!");
                        return;
                    }

                    if (!isBaseAsset)
                    {
                        return;
                    }

                    if (EditorUtility.DisplayDialog("WARNING", "This file is Base. Would you like to add it as theirs?", "Yes", "No"))
                    {
                        BaseObject.IsBaseObject = false;
                        BaseObject = null;
                        break;
                    }

                    return;
                }

                case ValidationError.InContexts:
                {
                    if (BaseObject != null && !isBaseAsset)
                    {
                        return;
                    }

                    // update base
                    if (BaseObject != null)
                    {
                        BaseObject.IsBaseObject = false;
                    }

                    BaseObject = Assets.FirstOrDefault(asset => asset.Path == newAsset.Path);

                    if (BaseObject != null)
                    {
                        BaseObject.IsBaseObject = true;
                    }

                    return;
                }
                case ValidationError.NoError:
                {
                    if (BaseObject == null || isBaseAsset)
                    {
                        if (BaseObject != null)
                        {
                            BaseObject.IsBaseObject = false;
                        }

                        BaseObject = newAsset;
                        BaseObject.IsBaseObject = true;
                    }
                    break;
                }

                default:
                {
                    throw new ArgumentOutOfRangeException();
                }
            }

            Assets.Add(newAsset);
            UpdateDiff(newAsset);
            if (!batching)
            {
                UpdateDependent(newAsset);
                UpdateAssets(Assets);
            }
        }
        #endregion

        protected override void UpdateDiff(Asset asset)
        {
            if (asset == null || BaseObject == null || !asset.Object || asset.IsMerged)
            {
                asset.DiffState = AssetDiffState.None;
                asset.MetaDiffState = AssetDiffState.None;
                return;
            }

            if (asset.IsBaseObject)
            {
                asset.DiffState = AssetDiffState.BaseObject;
                asset.MetaDiffState = AssetDiffState.BaseObject;
                return;
            }

            var result = DiffManager.CompareFilesBinary(BaseObject.Path, asset.Path);
            asset.DiffState = result.HasValue
                ? result.Value 
                    ? AssetDiffState.SameAsBaseObject 
                    : AssetDiffState.NotTheSameAsBaseObject
                : AssetDiffState.None;

            result = DiffManager.CompareMetaFiles(BaseObject.MetaPath, asset.MetaPath);
            asset.MetaDiffState = result.HasValue
                ? result.Value 
                    ? AssetDiffState.SameAsBaseObject 
                    : AssetDiffState.NotTheSameAsBaseObject
                : AssetDiffState.None;
        }
        
        /// <summary>
        /// Returns text description and color coding for asset diff states:
        /// - Cyan: Merged asset
        /// - Gray: Both states are None (error)
        /// - Orange: Both main and meta files differ from base
        /// - Red: Main file differs from base
        /// - Magenta: Meta file differs from base
        /// - Yellow: Base object (one or both are base)
        /// - Green: Both match base
        /// - Light green: One matches base, other is None
        /// - Default: GUI.color for all other cases
        /// </summary>
        protected override (string, Color)? GetAssetStateText(Asset asset)
        {
            if (asset.IsMerged)
            {
                return ("Asset merged", Color.cyan);
            }

            var diff = asset.DiffState;
            var meta = asset.MetaDiffState;

            if (diff == AssetDiffState.None && meta == AssetDiffState.None)
            {
                return ("Error: Missing states", GUI.color);
            }

            if (diff == AssetDiffState.NotTheSameAsBaseObject && meta == AssetDiffState.NotTheSameAsBaseObject)
            {
                return ("Asset and Meta mismatch", new Color(1f, 0.5f, 0f)); // Orange
            }

            if (diff == AssetDiffState.NotTheSameAsBaseObject)
            {
                return ("Asset mismatch", Color.red);
            }

            if (meta == AssetDiffState.NotTheSameAsBaseObject)
            {
                return ("Meta mismatch", Color.magenta);
            }

            if (diff == AssetDiffState.BaseObject || meta == AssetDiffState.BaseObject)
            {
                return ("Base object", Color.yellow);
            }

            if (diff == AssetDiffState.SameAsBaseObject && meta == AssetDiffState.SameAsBaseObject)
            {
                return ("Matches Base", Color.green);
            }

            if ((diff == AssetDiffState.SameAsBaseObject && meta == AssetDiffState.None)
                || (diff == AssetDiffState.None && meta == AssetDiffState.SameAsBaseObject))
            {
                return ("Partial match", new Color(0.5f, 1f, 0.5f)); // Light green
            }

            return null;
        }

        private enum ValidationError
        {
            NoError,
            Null,
            NotAFile,
            BaseObject,
            InContexts,
        }

        private ValidationError ValidateAsset(Asset asset)
        {
            if (asset == null)
            {
                return ValidationError.Null;
            }

            if (string.IsNullOrEmpty(asset.Path))
            {
                return ValidationError.NotAFile;
            }

            if (!File.Exists(asset.Path))
            {
                return ValidationError.NotAFile;
            }

            if (BaseObject?.Path == asset.Path)
            {
                Debug.LogError($"File {asset.Path} is the base object.");
                return ValidationError.BaseObject;
            }

            if (Assets.Any(ctx => ctx.Path == asset.Path))
            {
                Debug.LogError($"File {asset.Path} has already added.");
                return ValidationError.InContexts;
            }

            return ValidationError.NoError;
        }

        private void UpdateDependents(bool isEnable)
        {
            ShowEmptyDependencyText = isEnable;
            if (isEnable)
            {
                foreach (var asset in Assets)
                {
                    UpdateDependent(asset);
                }
            }
            else
            {
                foreach (var asset in Assets)
                {
                    asset.Dependencies = new List<Asset>();
                }
            }
        }

        private void UpdateDependent(Asset asset)
        {
            using var measure = Profiler.Measure("UpdateDependent");
            if (!ShowDependents || asset == null || asset.Object == null)
            {
                return;
            }

            var usedBy = SearchHelperService.FindUsedBy(asset.Object, IsCacheUsed);
            asset.Dependencies = usedBy.Dependencies;
        }



        #region Handlers
        private void DiffButtonPressedHandler(Asset asset)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Asset"), false, () => { InvokeDiffTool(BaseObject?.Path, BaseObject?.Path, asset?.Path, asset?.Path); });
            menu.AddItem(new GUIContent("Meta"), false, () => { InvokeDiffTool(BaseObject?.MetaPath, BaseObject?.MetaPath, asset?.MetaPath, asset?.MetaPath); });
            menu.ShowAsContext();
        }

        private void ComparandButtonPressedHandler(Asset asset)
        {
            if (asset.IsBaseObject)
            {
                asset.IsBaseObject = false;
                BaseObject = null;
            }
            else
            {
                Assets.ForEach(asset =>
                {
                    asset.IsBaseObject = false;
                    asset.MetaDiffState = AssetDiffState.None;
                });

                asset.IsBaseObject = true;
                BaseObject = asset;
            }

            DiffManager.UpdateState();
        }

        private void RemoveButtonPressedHandler(Asset asset)
        {
            Assets.RemoveAll(match => match.Path == asset.Path);
        }

        private void SelectedButtonPressedHandler(Asset asset)
        {
            if (asset.IsBaseObject)
            {
                asset.IsSelected = true;
            }
            else
            {
                asset.IsSelected = !asset.IsSelected;
            }
        }
        #endregion
    }
}
