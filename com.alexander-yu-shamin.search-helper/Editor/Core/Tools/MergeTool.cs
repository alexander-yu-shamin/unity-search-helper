using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SearchHelper.Editor.Core;
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
        protected override bool IsIgnoredFilesSupported { get; set; } = false;
        protected override bool ShouldMainObjectsBeSorted { get; set; } = true;

        private ObjectContext BaseObject { get; set; }
        private List<ObjectContext> Contexts { get; set; } = new List<ObjectContext>();
        protected override IEnumerable<ObjectContext> Data => Contexts;

        private Vector2 ScrollPosition { get; set; }

        public override void AssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
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
                EGuiKit.Button(BaseObject != null && !Contexts.IsNullOrEmpty(), "Clear", () =>
                {
                    BaseObject = null;
                    Contexts = null;
                });

                EGuiKit.FlexibleSpace();
                EGuiKit.Button(BaseObject != null && BaseObject.ShouldBeShown && !Contexts.IsNullOrEmpty(), "Merge", () =>
                {
                    Merge(BaseObject, Contexts);
                });

                DrawHeaderControls();
            });

            EGuiKit.Space(HeaderPadding);

            if (Contexts.IsNullOrEmpty())
            {
                return;
            }

            ScrollPosition = EGuiKit.ScrollView(ScrollPosition, () =>
            {
                foreach (var context in Contexts)
                {
                    DrawElement(context);
                }
            });
        }

        private void Merge(ObjectContext baseObject, List<ObjectContext> contexts)
        {
            if (contexts.IsNullOrEmpty())
            {
                return;
            }

            if (baseObject == null)
            {
                return;
            }

            AssetDatabase.StartAssetEditing();

            foreach (var context in contexts.Where(context => !context.IsBaseObject && context.IsSelected && context.ShouldBeShown))
            {
                if (context == null)
                {
                    continue;
                }

                Merge(baseObject, context);

                File.Delete(context.Path);
                File.Delete(context.MetaPath);
                context.IsMerged = true;
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void Merge(ObjectContext baseObject, ObjectContext theirsObject)
        {
            var dependencies = SearchHelperService.FindUsedBy(theirsObject.Object);
            foreach (var dependency in dependencies.Dependencies)
            {
                var dependencyObject = new ObjectContext(dependency);

                if (!string.IsNullOrEmpty(dependencyObject.Path))
                {
                    if (File.Exists(dependencyObject.Path))
                    {
                        var text = File.ReadAllText(dependencyObject.Path);
                        var replaced = text.Replace(theirsObject.Guid, baseObject.Guid);
                        File.WriteAllText(dependencyObject.Path, replaced);
                    }
                }

                if (!string.IsNullOrEmpty(dependencyObject.MetaPath))
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

        private void DrawElement(ObjectContext context)
        {
            if (context == null)
            {
                return;
            }

            if (!context.ShouldBeShown)
            {
                return;
            }

            EGuiKit.Horizontal(() =>
            {
                context.IsSelected = EditorGUILayout.ToggleLeft("Use for merge", context.IsSelected, GUILayout.Width(100));
                EGuiKit.Button("Remove", () =>
                {
                    Contexts.RemoveAll(match => match.Path == context.Path);
                }, GUILayout.Width(100));

                EGuiKit.Button(context.IsBaseObject ? "Base" : "Theirs", () =>
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
                }, GUILayout.Width(100));

                EGuiKit.Color(GetColor(context), () =>
                {
                    EditorGUILayout.ObjectField(context.Object, typeof(Object), true, GUILayout.Width(SelectedObjectWidth));
                });

                EGuiKit.Color(context.IsMerged ? Color.cyan : GUI.color, () =>
                {
                    EGuiKit.Button(EditorGUIUtility.IconContent(InspectorIconName), () =>
                    {
                        OpenProperty(context);
                    }, GUILayout.Width(ContentHeight), GUILayout.Height(ContentHeight));

                    EditorGUILayout.LabelField("GUID:", GUILayout.Width(40));
                    EditorGUILayout.TextArea(context.IsMerged ? "MERGED" : context.Guid, GUILayout.Width(GuidTextAreaWidth));
                    EGuiKit.Button(EditorGUIUtility.IconContent(FolderIconName), () =>
                    {
                        if (!string.IsNullOrEmpty(context.Path))
                        {
                            EditorUtility.RevealInFinder(context.Path);
                        }
                    }, GUILayout.Width(ContentHeight), GUILayout.Height(ContentHeight));

                    EditorGUILayout.LabelField("Path:", GUILayout.Width(40));
                    EditorGUILayout.TextArea(context.Path, GUILayout.ExpandWidth(true));
                });
            }, GUI.skin.box);
        }

        private Color GetColor(ObjectContext context)
        {
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

            var areMetasEqual = SearchHelperService.GetFileHashSHA256(BaseObject.MetaPath, 2) == SearchHelperService.GetFileHashSHA256(context.MetaPath, 2);
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

            var baseObjectMetaHash = SearchHelperService.GetFileHashSHA256(BaseObject.MetaPath, 2);

            foreach (var context in contexts)
            {
                if (!context.Object)
                {
                    context.State = ObjectState.None;
                    continue;
                }

                if (context.IsMerged)
                {
                    continue;
                    context.State = ObjectState.None;
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
                    var hash = SearchHelperService.GetFileHashSHA256(context.MetaPath, 2);
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
    }
}
