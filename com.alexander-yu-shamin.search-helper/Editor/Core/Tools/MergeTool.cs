using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        private class MergeObjectContext : ObjectContext
        {
            public bool IsSelected { get; set; } = true;
            public bool IsBaseObject { get; set; } = false;
            public bool IsMetaAsBaseObject { get; set; } = false;
            public string MetaPath => Path + ".meta";

            public MergeObjectContext(ObjectContext context) : base(context)
            {
            }
        }

        public override bool IsShowFoldersSupported { get; set; } = false;

        private MergeObjectContext BaseObject { get; set; }

        private List<MergeObjectContext> Contexts { get; set; } = new List<MergeObjectContext>();
        protected override IEnumerable<ObjectContext> Data => Contexts;

        private void OnPostprocessAllAssets()
        {
            if (BaseObject != null && !Contexts.IsNullOrEmpty())
            {
                CompareWithBaseObject(Contexts, BaseObject);
            }
        }

        public override void GetDataFromAnotherTool(List<ObjectContext> contexts)
        {
            Contexts = new List<MergeObjectContext>();

            foreach (var context in contexts)
            {
                AddObjectToMerge(context.Object, true);

                foreach (var dependency in context.Dependencies)
                {
                    AddObjectToMerge(dependency.Object, false);
                }
            }
        }

        public override void Draw(Rect windowRect)
        {
            EGuiKit.Horizontal(() =>
            {
                var newObject = EditorGUILayout.ObjectField(BaseObject?.Object, typeof(Object), true,
                    GUILayout.Width(SelectedObjectWidth));
                if (newObject != BaseObject?.Object)
                {
                    AddObjectToMerge(newObject, true);
                }

                EGuiKit.Button("Add Selected Object as Base Object", () => { AddObjectToMerge(Selection.activeObject, true); });
                EGuiKit.Button("Add Selected Object for Merge", () => { AddObjectToMerge(Selection.activeObject);});
                EGuiKit.FlexibleSpace();
                EGuiKit.Button(BaseObject != null && !Contexts.IsNullOrEmpty(), "Merge", () =>
                {
                    Merge(BaseObject, Contexts);
                });

                DrawHeaderControls();
            });

            if (Contexts.IsNullOrEmpty())
            {
                return;
            }

            EGuiKit.Vertical(() =>
            {
                foreach (var context in Contexts)
                {
                    DrawElement(context);
                }
            });
        }

        private void Merge(MergeObjectContext baseObject, List<MergeObjectContext> contexts)
        {
            if (contexts.IsNullOrEmpty())
            {
                return;
            }

            AssetDatabase.StartAssetEditing();

            foreach (var context in contexts)
            {
                if (context == null)
                {
                    continue;
                }

                Merge(baseObject, context);

                File.Delete(context.Path);
                File.Delete(context.MetaPath);
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void Merge(MergeObjectContext baseObject, MergeObjectContext theirsObject)
        {
            var dependencies = SearchHelperService.FindUsedBy(theirsObject.Object);
            foreach (var dependency in dependencies.Dependencies)
            {
                var dependencyObject = new MergeObjectContext(dependency);

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

        private void DrawElement(MergeObjectContext context)
        {
            EGuiKit.Horizontal(() =>
            {
                context.IsExpanded = EditorGUILayout.ToggleLeft("Use for merge", context.IsExpanded, GUILayout.Width(100));
                EGuiKit.Button("Remove", () =>
                {
                    Contexts.RemoveAll(match => match.Path == context.Path);
                }, GUILayout.Width(100));

                EGuiKit.Color(context.IsMetaAsBaseObject ? Color.green : Color.red, () =>
                {
                    EditorGUILayout.ObjectField(context.Object, typeof(Object), true, GUILayout.Width(SelectedObjectWidth));
                });

                EditorGUILayout.LabelField("GUID:", GUILayout.Width(40));
                EditorGUILayout.TextArea(context.Guid, GUILayout.Width(GuidTextAreaWidth));
                EditorGUILayout.LabelField("Path:", GUILayout.Width(40));
                EditorGUILayout.TextArea(context.Path, GUILayout.ExpandWidth(true));
            });
        }

        public override void Run(Object selectedObject, Settings settings)
        {
            if (selectedObject != null)
            {
                AddObjectToMerge(selectedObject);
            }
        }

        private void AddObjectToMerge(Object selectedObject, bool isBaseObject = false)
        {
            if (selectedObject == null)
            {
                return;
            }

            if (Contexts == null)
            {
                return;
            }

            if (BaseObject == null || isBaseObject)
            {
                var context = ToMergeObjectContext(selectedObject);
                var validationError = ValidateMergeObjectContext(context);

                switch (validationError)
                {
                    case ValidationError.InContexts:
                    case ValidationError.BaseObject:
                    case ValidationError.NoError:
                    {
                        UpdateBaseObject(context);
                        break;
                    }
                    default:
                    {
                        Debug.LogError($"The object {selectedObject.name} can't be used for merge. It a part of another object.");
                        BaseObject = null;
                        break;
                    }
                }
            }
            else
            {
                var context = ToMergeObjectContext(selectedObject);
                if (ValidateMergeObjectContext(context) == ValidationError.NoError)
                {
                    CompareWithBaseObject(context, BaseObject);
                    Contexts.Add(context);
                    UpdateData(Contexts);
                }
            }
        }

        private void CompareWithBaseObject(MergeObjectContext context, MergeObjectContext baseContext)
        {
            if (!File.Exists(baseContext.MetaPath))
            {
                Debug.LogError($"Cannot find path {baseContext.MetaPath}.");
                return;
            }

            if (!File.Exists(context.MetaPath))
            {
                context.IsMetaAsBaseObject = false;
                Debug.LogError($"Cannot find path {context.MetaPath}.");
                return;
            }

            var areMetasEqual = GetFileHash(baseContext.MetaPath, 2) == GetFileHash(context.MetaPath, 2);
            context.IsMetaAsBaseObject = areMetasEqual;
        }

        private void CompareWithBaseObject(List<MergeObjectContext> contexts, MergeObjectContext baseContext)
        {
            if (!File.Exists(baseContext.MetaPath))
            {
                Debug.LogError($"Cannot find path {baseContext.MetaPath}.");
                return;
            }

            var baseContextMetaHash = GetFileHash(baseContext.MetaPath, 2);

            foreach (var context in contexts)
            {
                if (!File.Exists(context.MetaPath))
                {
                    context.IsMetaAsBaseObject = false;
                    Debug.LogError($"Cannot find path {context.MetaPath}.");
                    continue;
                }

                var hash = GetFileHash(context.MetaPath, 2);
                context.IsMetaAsBaseObject = hash == baseContextMetaHash;
            }
        }

        private void UpdateBaseObject(MergeObjectContext baseObject)
        {
            BaseObject = baseObject;
            Contexts.RemoveAll(match => match.Path == baseObject.Path);
            CompareWithBaseObject(Contexts, BaseObject);
        }

        private enum ValidationError
        {
            NoError,
            Null,
            NotAFile,
            BaseObject,
            InContexts,
        }

        private ValidationError ValidateMergeObjectContext(MergeObjectContext context)
        {
            if (context == null)
            {
                return ValidationError.Null;
            }

            if (string.IsNullOrEmpty(context.Path))
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

        private MergeObjectContext ToMergeObjectContext(Object obj)
        {
            return new MergeObjectContext(ObjectContext.ToObjectContext(obj));
        }

        static string GetFileHash(string path, int skipLines)
        {
            using var sha = SHA256.Create();
            using var reader = new StreamReader(path, Encoding.UTF8);

            for (int i = 0; i < skipLines; i++)
            {
                reader.ReadLine();
            }

            var remaining = reader.ReadToEnd();
            var bytes = Encoding.UTF8.GetBytes(remaining);
            var hash = sha.ComputeHash(bytes);

            return Convert.ToBase64String(hash);
        }
    }
}
