using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SearchHelper.Editor.Core;
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
        protected override bool IsShowingFoldersSupported { get; set; } = false;
        protected override bool IsFilteringSupported { get; set; } = false;
        protected override bool ShouldMainObjectsBeSorted { get; set; } = true;
        protected override string EmptyObjectContextText { get; set; } = "This object is not referenced anywhere in the project.";

        private ObjectContext BaseObject { get; set; }
        private List<ObjectContext> Contexts { get; set; } = new();
        private Model DrawModel { get; set; }
        private bool ShowDependents { get; set; } = false;

        private readonly HashSet<string> _defaultLines = new() { "assetBundleName", "assetBundleVariant", "SpriteID", "userData" };
        private HashSet<string> IgnoredLines { get; set; } = new HashSet<string>();

        protected override IEnumerable<ObjectContext> Data => Contexts;

        public override void AssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            using var measure = Profiler.Measure("AssetChanged");
            foreach (var context in Contexts)
            {
                if (context.Object != null)
                {
                    context.IsMerged = false;
                }
            }

            CompareWithBaseObject(Contexts);
        }

        public override void GetDataFromAnotherTool(ObjectContext context)
        {
            if (context == null)
            {
                return;
            }

            if (context.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            BaseObject = null;
            Contexts = new List<ObjectContext>();

            if (Contexts.Contains(context))
            {
                return;
            }

            if (Contexts.Any(element => element.Path == context.Path))
            {
                return;
            }

            AddObjectToMergeAsBase(context.Object);

            foreach (var dependency in context.Dependencies.Where(dependency => dependency.Path != context.Path))
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
                EGuiKit.Button(BaseObject != null && BaseObject.ShouldBeShown && !Contexts.IsNullOrEmpty(), "Merge", () =>
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
                DrawDependencies = ShowDependents,
                DrawObjectWithEmptyDependencies = true,
                DrawMergeButtons = true,
                DrawState = true,
                DrawEmptyDependency = ShowDependents,

                GetState = GetObjectState,
                GetObjectFieldColor = GetObjectFieldColor,
                OnSelectedButtonPressed = SelectedButtonPressedHandler,
                OnRemoveButtonPressed = RemoveButtonPressedHandler,
                OnComparandButtonPressed = ComparandButtonPressedHandler,
                OnDiffButtonPressed = DiffButtonPressedHandler
            };

            DrawModel.DrawDependencies = ShowDependents;
            DrawModel.DrawEmptyDependency = ShowDependents;

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
                        Contexts.ForEach(context => context.IsSelected = context.ShouldBeShown);
                    });

                menu.AddItem(new GUIContent("Unselect All"), false,
                    () =>
                    {
                        Contexts.ForEach(context => context.IsSelected = false);
                    });

                menu.AddItem(new GUIContent("Select Similar"), false,
                    () =>
                    {
                        Contexts.ForEach(context => context.IsSelected = context.State == ObjectState.SameAsBaseObject && context.ShouldBeShown);
                    });

                menu.ShowAsContext();
            }
        }

        private void Merge(ObjectContext baseObject, List<ObjectContext> contexts, bool isCacheUsed)
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

            foreach (var context in contexts.Where(context => context is { IsBaseObject: false, IsSelected: true, ShouldBeShown: true }))
            {
                List<ObjectContext> dependencies = null;
                if (!dependencyMap.TryGetValue(context.Path, out dependencies))
                {
                    dependencies = SearchHelperService.FindUsedBy(context.Object, isCacheUsed)?.Dependencies;
                }

                if(dependencies == null)
                {
                    Debug.LogError($"Can't find dependencies for {context.Path}");
                    continue;
                }

                Merge(baseObject, context, dependencies);

                using (Profiler.Measure($"Merge::Delete {context.Path}"))
                {
                    File.Delete(context.Path);
                }

                using (Profiler.Measure($"Merge::Delete {context.MetaPath}"))
                {
                    File.Delete(context.MetaPath);
                }
                context.IsMerged = true;
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void Merge(ObjectContext baseObject, ObjectContext theirsObject, List<ObjectContext> dependencies)
        {
            using var measure = Profiler.Measure("MergeElement");
            foreach (var dependency in dependencies)
            {
                var dependencyObject = new ObjectContext(dependency);

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

        private Color GetObjectFieldColor(ObjectContext context)
        {
            if (context.IsMerged)
            {
                return Color.cyan;
            }

            switch (context.State)
            {
                case ObjectState.BaseObject:
                    return Color.yellow;
                case ObjectState.SameAsBaseObject:
                    return Color.green;
                case ObjectState.NotTheSameAsBaseObject:
                    return Color.red;
                case ObjectState.None:
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

            Contexts ??= new List<ObjectContext>();

            var newBaseObject = ObjectContext.ToObjectContext(selectedObject);
            var validationError = ValidateObjectContext(newBaseObject);

            switch (validationError)
            {
                case ValidationError.InContexts:
                {
                    BaseObject.IsBaseObject = false;
                    BaseObject = Contexts.FirstOrDefault(context => context.Path == newBaseObject.Path);
                    if (BaseObject != null)
                    {
                        BaseObject.IsBaseObject = true;
                        BaseObject.State = ObjectState.BaseObject;
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
                BaseObject.State = ObjectState.None;
                BaseObject = null;
            }

            Contexts.Add(newBaseObject);
            BaseObject = newBaseObject;
            BaseObject.State = ObjectState.BaseObject;
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

            Contexts ??= new List<ObjectContext>();

            var mergeObject = ObjectContext.ToObjectContext(selectedObject);
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
            UpdateData(Contexts);
        }

        private void CompareWithBaseObject(ObjectContext context)
        {
            if (BaseObject == null || context == null)
            {
                return;
            }

            if (!File.Exists(BaseObject.MetaPath))
            {
                Debug.LogError($"Cannot find base metafile [{BaseObject.MetaPath}].");
                return;
            }

            if (!File.Exists(context.MetaPath))
            {
                context.State = ObjectState.None;
                Debug.LogError($"Cannot find theirs metafile [{context.MetaPath}].");
                return;
            }

            var areMetasEqual = SearchHelperService.GetFileHashSHA256(BaseObject.MetaPath, 2, IgnoredLines) == SearchHelperService.GetFileHashSHA256(context.MetaPath, 2, IgnoredLines);
            context.State = areMetasEqual ? ObjectState.SameAsBaseObject : ObjectState.NotTheSameAsBaseObject;
        }

        private void CompareWithBaseObject(List<ObjectContext> contexts)
        {
            if (contexts.IsNullOrEmpty())
            {
                return;
            }

            if (BaseObject == null)
            {
                contexts.ForEach(context => context.State = ObjectState.None);
                return;
            }

            if (!File.Exists(BaseObject.MetaPath))
            {
                Debug.LogError($"Cannot find base metafile [{BaseObject.MetaPath}].");
                return;
            }

            var baseObjectMetaHash = SearchHelperService.GetFileHashSHA256(BaseObject.MetaPath, 2, IgnoredLines);

            foreach (var context in contexts)
            {
                if (!context.Object)
                {
                    context.State = ObjectState.None;
                    continue;
                }

                if (context.IsMerged)
                {
                    context.State = ObjectState.None;
                    continue;
                }

                if (!File.Exists(context.MetaPath))
                {
                    context.State = ObjectState.None;
                    Debug.LogError($"Cannot find theirs metafile [{context.MetaPath}].");
                    continue;
                }

                if (context.IsBaseObject)
                {
                    context.State = ObjectState.BaseObject;
                }
                else
                {
                    var hash = SearchHelperService.GetFileHashSHA256(context.MetaPath, 2, IgnoredLines);
                    context.State = hash == baseObjectMetaHash
                        ? ObjectState.SameAsBaseObject
                        : ObjectState.NotTheSameAsBaseObject;
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

        private ValidationError ValidateObjectContext(ObjectContext context)
        {
            if (context == null)
            {
                return ValidationError.Null;
            }

            if (string.IsNullOrEmpty(context.Path))
            {
                return ValidationError.NotAFile;
            }

            if (!File.Exists(context.Path))
            {
                return ValidationError.NotAFile;
            }

            if (BaseObject?.Path == context.Path)
            {
                Debug.LogError($"File {context.Path} is the base object.");
                return ValidationError.BaseObject;
            }

            if (Contexts.Any(ctx => ctx.Path == context.Path))
            {
                Debug.LogError($"File {context.Path} has already added.");
                return ValidationError.InContexts;
            }

            return ValidationError.NoError;
        }

        private void UpdateDependents()
        {
            foreach (var context in Contexts)
            {
                UpdateDependent(context);
            }
        }

        private void UpdateDependent(ObjectContext context)
        {
            using var measure = Profiler.Measure("UpdateDependent");
            if (!ShowDependents)
            {
                return;
            }

            if (context == null)
            {
                return;
            }

            if (context.Object == null)
            {
                return;
            }

            var usedBy = SearchHelperService.FindUsedBy(context.Object, IsCacheUsed);
            context.Dependencies = usedBy.Dependencies;
        }

        private (string, Color)? GetObjectState(ObjectContext context)
        {
            if (context.IsMerged)
            {
                return ("Merged", Color.cyan);
            }

            if (context.State == ObjectState.NotTheSameAsBaseObject)
            {
                return ("Meta mismatch", Color.red);
            }

            return null;
        }

        private void DiffButtonPressedHandler(ObjectContext context)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Asset"), false, () => { InvokeDiffTool(BaseObject.Path, BaseObject.Path, context.Path, context.Path); });
            menu.AddItem(new GUIContent("Meta"), false, () => { InvokeDiffTool(BaseObject.MetaPath, BaseObject.MetaPath, context.MetaPath, context.MetaPath); });
            menu.ShowAsContext();
        }

        private void ComparandButtonPressedHandler(ObjectContext context)
        {
            if (context.IsBaseObject)
            {
                context.IsBaseObject = false;
                BaseObject = null;
            }
            else
            {
                Contexts.ForEach(context =>
                {
                    context.IsBaseObject = false;
                    context.State = ObjectState.None;
                });
                context.IsBaseObject = true;
                BaseObject = context;
            }

            CompareWithBaseObject(Contexts);
        }

        private void RemoveButtonPressedHandler(ObjectContext context)
        {
            Contexts.RemoveAll(match => match.Path == context.Path);
        }

        private void SelectedButtonPressedHandler(ObjectContext context)
        {
            if (context.IsBaseObject)
            {
                context.IsSelected = true;
            }
            else
            {
                context.IsSelected = !context.IsSelected;
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
