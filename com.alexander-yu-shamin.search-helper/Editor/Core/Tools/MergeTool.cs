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
        protected override string EmptyObjectContextText { get; set; } = "This object is not referenced anywhere in the project.";

        private Asset BaseObject { get; set; }
        private List<Asset> Contexts { get; set; } = new();
        private Model DrawModel { get; set; }
        private bool ShowDependents { get; set; } = false;

        private readonly HashSet<string> _defaultLines = new() { "assetBundleName", "assetBundleVariant", "SpriteID", "userData" };
        private HashSet<string> IgnoredLines { get; set; } = new HashSet<string>();

        protected override SearchHelperWindow.ToolType CurrentToolType { get; set; } =
            SearchHelperWindow.ToolType.MergeTool;

        protected override IEnumerable<Asset> Data => Contexts;

        public override void AssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            using var measure = Profiler.Measure("AssetChanged");
            foreach (var asset in Contexts)
            {
                if (asset.Object != null)
                {
                    asset.IsMerged = false;
                }
            }

            CompareWithBaseObject(Contexts);
        }

        public override void GetDataFromAnotherTool(SearchHelperWindow.ToolType from,
            SearchHelperWindow.ToolType to, Asset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (asset.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            BaseObject = null;
            Contexts = new List<Asset>();

            if (Contexts.Contains(asset))
            {
                return;
            }

            if (Contexts.Any(element => element.Path == asset.Path))
            {
                return;
            }

            AddObjectToMergeAsBase(asset.Object);

            foreach (var dependency in asset.Dependencies.Where(dependency => dependency.Path != asset.Path))
            {
                AddObjectToMergeAsTheirs(dependency.Object);
            }
        }

        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() =>
            {
                EGuiKit.Button("Add Selected Object as Base", () => { AddObjectToMergeAsBase(Selection.activeObject); });
                EGuiKit.Button("Add Selected Object as Theirs", () => { AddObjectToMergeAsTheirs(Selection.activeObject);});

                EGuiKit.Button(BaseObject != null || !Contexts.IsNullOrEmpty(), "Clear", () =>
                {
                    BaseObject = null;
                    Contexts = null;
                });

                DrawSelectButton();

                EGuiKit.FlexibleSpace();
                EGuiKit.Button(BaseObject != null && IsMainAssetVisible(BaseObject) && !Contexts.IsNullOrEmpty(), "Merge", () =>
                {
                    Merge(BaseObject, Contexts, IsCacheUsed);
                });

                DrawHeaderControls();
            });

            EGuiKit.Space(HeaderPadding);

            if (Contexts.IsNullOrEmpty())
            {
                return;
            }

            DrawModel ??= new Model()
            {
                DrawMergeButtons = true,
                DrawState = true,

                GetState = GetObjectState,
                GetObjectFieldColor = GetObjectFieldColor,
                OnSelectedButtonPressed = SelectedButtonPressedHandler,
                OnRemoveButtonPressed = RemoveButtonPressedHandler,
                OnComparandButtonPressed = ComparandButtonPressedHandler,
                OnDiffButtonPressed = DiffButtonPressedHandler
            };


            EGuiKit.Vertical(() => DrawVirtualScroll(windowRect, Contexts, DrawModel));
        }

        public override void Run(Object selectedObject)
        {
            if (selectedObject != null)
            {
                AddObjectToMergeAsBase(selectedObject);
            }
        }

        public override void Run()
        {
        }

        protected override void AddSettingsContextMenu(GenericMenu menu)
        {
            base.AddSettingsContextMenu(menu);
            menu.AddItem(new GUIContent("Show Dependents"), ShowDependents, () =>
            {
                ShowDependents = !ShowDependents;
                UpdateDependents();
            });

            var hashset = new HashSet<string>();
            hashset.UnionWith(_defaultLines);
            hashset.UnionWith(IgnoredLines);

            foreach (var ignoredLine in hashset)
            {
                AddItem(ignoredLine);
            }

            menu.AddItem(new GUIContent($"Ignore Line in Diff/Add your line"), false, () =>
            {
                InputDialog.Show("Add Ignore Line", "", result =>
                {
                    if (!string.IsNullOrEmpty(result))
                    {
                        IgnoredLines.Add(result);
                    }
                });
            });


            void AddItem(string s)
            {
                menu.AddItem(new GUIContent($"Ignore Line in Diff/{s}"), IgnoredLines.Contains(s), () =>
                {
                    if (IgnoredLines.Contains(s))
                    {
                        IgnoredLines.Remove(s);
                    }
                    else
                    {
                        IgnoredLines.Add(s);
                    }

                    CompareWithBaseObject(Contexts);
                });
            }
        }

        private void DrawSelectButton()
        {
            var content = new GUIContent($"Selection");
            if (EditorGUILayout.DropdownButton(content, FocusType.Passive, GUI.skin.button, GUILayout.Width(75)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Select All"), false,
                    () =>
                    {
                        Contexts.ForEach(asset => asset.IsSelected = IsMainAssetVisible(asset));
                    });

                menu.AddItem(new GUIContent("Unselect All"), false,
                    () =>
                    {
                        Contexts.ForEach(asset => asset.IsSelected = false);
                    });

                menu.AddItem(new GUIContent("Select Similar"), false,
                    () =>
                    {
                        Contexts.ForEach(asset => asset.IsSelected = asset.MergeState == AssetMergeState.SameAsBaseObject && IsMainAssetVisible(asset));
                    });

                menu.ShowAsContext();
            }
        }

        private void Merge(Asset baseObject, List<Asset> contexts, bool isCacheUsed)
        {
            using var measure = Profiler.Measure("Merge");
            if (contexts.IsNullOrEmpty())
            {
                return;
            }

            if (baseObject == null)
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

        private void InvokeDiffTool(string leftTitle, string leftFile, string rightTitle, string rightFile)
        {
            if (string.IsNullOrEmpty(leftFile))
            {
                Debug.LogError($"LeftFile is null");
                return;
            }

            if (string.IsNullOrEmpty(leftFile))
            {
                Debug.LogError($"RightFile is null");
                return;
            }

            if (!File.Exists(leftFile))
            {
                Debug.LogError($"Can not find Left File");
                return;
            }

            if (!File.Exists(rightFile))
            {
                Debug.LogError($"Can not find Right File");
                return;
            }

            EditorUtility.InvokeDiffTool(leftTitle, leftFile, rightTitle, rightFile, null, null);
        }

        private Color GetObjectFieldColor(Asset asset)
        {
            if (asset.IsMerged)
            {
                return Color.cyan;
            }

            switch (asset.MergeState)
            {
                case AssetMergeState.BaseObject:
                    return Color.yellow;
                case AssetMergeState.SameAsBaseObject:
                    return Color.green;
                case AssetMergeState.NotTheSameAsBaseObject:
                    return Color.red;
                case AssetMergeState.None:
                default:
                    return GUI.color;
            }
        }

        private void AddObjectToMergeAsBase(Object selectedObject)
        {
            if (selectedObject == null)
            {
                return;
            }

            Contexts ??= new List<Asset>();

            var newBaseObject = Asset.ToObjectContext(selectedObject);
            var validationError = ValidateObjectContext(newBaseObject);

            switch (validationError)
            {
                case ValidationError.InContexts:
                {
                    BaseObject.IsBaseObject = false;
                    BaseObject = Contexts.FirstOrDefault(asset => asset.Path == newBaseObject.Path);
                    if (BaseObject != null)
                    {
                        BaseObject.IsBaseObject = true;
                        BaseObject.MergeState = AssetMergeState.BaseObject;
                    }
                    return;
                }

                case ValidationError.NoError:
                {
                    break;
                }
                case ValidationError.BaseObject:
                case ValidationError.Null:
                case ValidationError.NotAFile:
                {
                    return;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException();
                }
            }

            if (BaseObject != null)
            {
                BaseObject.IsBaseObject = false;
                BaseObject.MergeState = AssetMergeState.None;
                BaseObject = null;
            }

            Contexts.Add(newBaseObject);
            BaseObject = newBaseObject;
            BaseObject.MergeState = AssetMergeState.BaseObject;
            BaseObject.IsBaseObject = true;
            UpdateDependent(BaseObject);

            CompareWithBaseObject(Contexts);
        }

        private void AddObjectToMergeAsTheirs(Object selectedObject)
        {
            if (selectedObject == null)
            {
                return;
            }

            Contexts ??= new List<Asset>();

            var mergeObject = Asset.ToObjectContext(selectedObject);
            var validationError = ValidateObjectContext(mergeObject);

            switch (validationError)
            {
                case ValidationError.NoError:
                {
                    break;
                }
                case ValidationError.BaseObject:
                {
                    if (EditorUtility.DisplayDialog("WARNING",
                            "This file is a base object. Would you like to add it to the merge list?", "Yes", "No"))
                    {
                        BaseObject.IsBaseObject = false;
                        BaseObject = null;
                    }
                    return;
                }
                case ValidationError.Null:
                case ValidationError.InContexts:
                case ValidationError.NotAFile:
                    return;
                default:
                {
                    throw new ArgumentOutOfRangeException();
                }
            }

            Contexts.Add(mergeObject);
            CompareWithBaseObject(mergeObject);
            UpdateDependent(mergeObject);
            UpdateAssets(Contexts);
        }

        private void CompareWithBaseObject(Asset asset)
        {
            if (BaseObject == null || asset == null)
            {
                return;
            }

            if (!File.Exists(BaseObject.MetaPath))
            {
                Debug.LogError($"Cannot find base metafile [{BaseObject.MetaPath}].");
                return;
            }

            if (!File.Exists(asset.MetaPath))
            {
                asset.MergeState = AssetMergeState.None;
                Debug.LogError($"Cannot find theirs metafile [{asset.MetaPath}].");
                return;
            }

            var areMetasEqual = SearchHelperService.GetFileHashSHA256(BaseObject.MetaPath, 2, IgnoredLines) == SearchHelperService.GetFileHashSHA256(asset.MetaPath, 2, IgnoredLines);
            asset.MergeState = areMetasEqual ? AssetMergeState.SameAsBaseObject : AssetMergeState.NotTheSameAsBaseObject;
        }

        private void CompareWithBaseObject(List<Asset> contexts)
        {
            if (contexts.IsNullOrEmpty())
            {
                return;
            }

            if (BaseObject == null)
            {
                contexts.ForEach(asset => asset.MergeState = AssetMergeState.None);
                return;
            }

            if (!File.Exists(BaseObject.MetaPath))
            {
                Debug.LogError($"Cannot find base metafile [{BaseObject.MetaPath}].");
                return;
            }

            var baseObjectMetaHash = SearchHelperService.GetFileHashSHA256(BaseObject.MetaPath, 2, IgnoredLines);

            foreach (var asset in contexts)
            {
                if (!asset.Object)
                {
                    asset.MergeState = AssetMergeState.None;
                    continue;
                }

                if (asset.IsMerged)
                {
                    asset.MergeState = AssetMergeState.None;
                    continue;
                }

                if (!File.Exists(asset.MetaPath))
                {
                    asset.MergeState = AssetMergeState.None;
                    Debug.LogError($"Cannot find theirs metafile [{asset.MetaPath}].");
                    continue;
                }

                if (asset.IsBaseObject)
                {
                    asset.MergeState = AssetMergeState.BaseObject;
                }
                else
                {
                    var hash = SearchHelperService.GetFileHashSHA256(asset.MetaPath, 2, IgnoredLines);
                    asset.MergeState = hash == baseObjectMetaHash
                        ? AssetMergeState.SameAsBaseObject
                        : AssetMergeState.NotTheSameAsBaseObject;
                }
            }
        }

        private enum ValidationError
        {
            NoError,
            Null,
            NotAFile,
            BaseObject,
            InContexts,
        }

        private ValidationError ValidateObjectContext(Asset asset)
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

            if (Contexts.Any(ctx => ctx.Path == asset.Path))
            {
                Debug.LogError($"File {asset.Path} has already added.");
                return ValidationError.InContexts;
            }

            return ValidationError.NoError;
        }

        private void UpdateDependents()
        {
            foreach (var asset in Contexts)
            {
                UpdateDependent(asset);
            }
        }

        private void UpdateDependent(Asset asset)
        {
            using var measure = Profiler.Measure("UpdateDependent");
            if (!ShowDependents)
            {
                return;
            }

            if (asset == null)
            {
                return;
            }

            if (asset.Object == null)
            {
                return;
            }

            var usedBy = SearchHelperService.FindUsedBy(asset.Object, IsCacheUsed);
            asset.Dependencies = usedBy.Dependencies;
        }

        private (string, Color)? GetObjectState(Asset asset)
        {
            if (asset.IsMerged)
            {
                return ("Merged", Color.cyan);
            }

            if (asset.MergeState == AssetMergeState.NotTheSameAsBaseObject)
            {
                return ("Meta mismatch", Color.red);
            }

            return null;
        }

        private void DiffButtonPressedHandler(Asset asset)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Asset"), false, () => { InvokeDiffTool(BaseObject.Path, BaseObject.Path, asset.Path, asset.Path); });
            menu.AddItem(new GUIContent("Meta"), false, () => { InvokeDiffTool(BaseObject.MetaPath, BaseObject.MetaPath, asset.MetaPath, asset.MetaPath); });
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
                Contexts.ForEach(asset =>
                {
                    asset.IsBaseObject = false;
                    asset.MergeState = AssetMergeState.None;
                });
                asset.IsBaseObject = true;
                BaseObject = asset;
            }

            CompareWithBaseObject(Contexts);
        }

        private void RemoveButtonPressedHandler(Asset asset)
        {
            Contexts.RemoveAll(match => match.Path == asset.Path);
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

        public class InputDialog : EditorWindow
        {
            private static string _inputText = "";
            private static Action<string> _onConfirm;

            public static void Show(string title, string initialValue, Action<string> callback)
            {
                _inputText = initialValue;
                _onConfirm = callback;

                var window = CreateInstance<InputDialog>();
                window.titleContent = new GUIContent(title);
                var size = new Vector2(400, 60);
                window.minSize = size;
                window.maxSize = size;
                window.position = new Rect(Screen.width / 2, Screen.height / 2, size.x, size.y);

                window.ShowModalUtility();
            }

            void OnGUI()
            {
                GUILayout.Label("Input:", EditorStyles.boldLabel);
                _inputText = EditorGUILayout.TextField(_inputText);

                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("OK", GUILayout.Width(100)))
                {
                    _onConfirm?.Invoke(_inputText);
                    Close();
                }

                if (GUILayout.Button("Cancel", GUILayout.Width(100)))
                {
                    Close();
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
