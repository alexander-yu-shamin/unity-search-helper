using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NUnit.Framework;
using Search.Helper.Runtime.Extensions;
using Search.Helper.Runtime.Helpers;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Search.Helper.Editor.Tools
{
    public class SearchHelperTool : EditorWindow
    {
        public class ObjectContext
        {
            public Object Object { get; set; }
            public string Path { get; set; }
            public string Guid { get; set; }
            public Dependencies Dependencies { get; set; }

            public bool IsValid => Object != null && !string.IsNullOrEmpty(Path);
        }

        public class Dependencies : List<ObjectContext>
        {
            public bool IsExpanded { get; set; }
            public string Filter { get; set; }

            public Dependencies()
            {
            }

            public Dependencies(IEnumerable<ObjectContext> objectContexts) : base(objectContexts)
            {
            }
        }

        private enum Panel
        {
            Uses,
            FindByGuid,
            UsedBy,
            Duplicate,
            Unused,
            UsesInBuild,
            AssetsByType
        }

        private const string WindowTitle = "Search Helper Tool";
        private const string WindowMenuItemName = "Window/Search/Open Search Helper Tool";
        private const string ContextMenuItemFindUsesName = "Assets/Search Helper Tool: Find Uses";
        private const string ContextMenuFindUsedByItemName = "Assets/Search Helper Tool: Find Used By";

        private const string ResourceString = "/Resources/";
        private const string EditorString = "/Editor/";

        private ObjectContext CurrentUsesObject { get; set; }
        private Object SelectedUsesObject { get; set; }
        public string CurrentGuid { get; set; }

        public ObjectContext CurrentGuidObject { get; set; }
        public bool? IsGuidObjectFound { get; set; }

        public List<ObjectContext> CurrentUsedByObjects { get; set; }
        public bool ShouldEditorAssetsBeIgnored { get; set; }
        public bool ShouldFindDependencies { get; set; } = true;

        public List<ObjectContext> CurrentUnusedObjects { get; set; }

        private Panel CurrentPanel { get; set; } = Panel.Uses;
        private Vector2 ScrollViewPos { get; set; } = Vector2.zero;

        private string[] PanelNames { get; set; }
        private Color ErrorColor => Color.red;
        private Color WarningColor => Color.yellow;

        [MenuItem(WindowMenuItemName)]
        public static SearchHelperTool OpenWindow()
        {
            return GetWindow<SearchHelperTool>(WindowTitle);
        }

        [MenuItem(ContextMenuItemFindUsesName)]
        public static void ShowUses()
        {
            var window = OpenWindow().ChangePanel(Panel.Uses);
            window?.SetCurrentObject(window?.FindDependencies(Selection.activeObject));
        }

        [MenuItem(ContextMenuFindUsedByItemName)]
        public static void ShowUsesBy()
        {
            var window = OpenWindow().ChangePanel(Panel.UsedBy);
            window?.SetCurrentObjects(window?.FindUsedBy(Selection.activeObject, window.ShouldFindDependencies),
                Selection.activeObject);
        }

        [MenuItem(ContextMenuItemFindUsesName, true)]
        [MenuItem(ContextMenuFindUsedByItemName, true)]
        public static bool ValidateActiveSelectedObject()
        {
            return Selection.activeObject;
        }

        public SearchHelperTool()
        {
            PanelNames = Enum.GetNames(typeof(Panel)).Select(element => element.AddSpacesBeforeUppercase()).ToArray();
        }

        private Dictionary<Object, ObjectContext> Dictionary { get; set; } = new();

        public IEnumerable<ObjectContext> FindAllAssets(string root = null)
        {
            EditorUtility.DisplayCancelableProgressBar("Search Helper Tool", "Find All Assets", 0f);

            var searchFilter = "t:Object";
            var searchDirs = new[] { "Assets" };
            if (!string.IsNullOrEmpty(root))
            {
                searchDirs = new[] { root };
            }

            var guids = AssetDatabase.FindAssets(searchFilter, searchDirs);

            var objects = guids.Select(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadMainAssetAtPath(path);

                return new ObjectContext()
                {
                    Guid = guid,
                    Path = path,
                    Object = obj,
                    Dependencies = new Dependencies()
                };
            });

            EditorUtility.DisplayCancelableProgressBar("Search Helper Tool", "Find All Assets", 100f);
            EditorUtility.ClearProgressBar();

            return objects;
        }

        public List<ObjectContext> FindUsedBy(Object obj, bool updatedDependencies = false)
        {
            var results = new List<ObjectContext>();
            var objects = FindAllAssets();
            var objectContexts = objects.ToList();

            if (!objectContexts.Any())
            {
                return results;
            }

            EditorUtility.DisplayCancelableProgressBar("Search Helper Tool", "Find All Assets", 0f);
            for (var i = 0; i < objectContexts.Count; i++)
            {
                var objectContext = objectContexts[i];

                if (obj == objectContext.Object)
                {
                    continue;
                }

                var dependencies = EditorUtility.CollectDependencies(new[] { objectContext.Object });
                if (dependencies.Any(element => element == obj))
                {
                    if (updatedDependencies)
                    {
                        objectContext.Dependencies = new Dependencies(ToObjectContexts(dependencies, obj));
                    }

                    results.Add(objectContext);
                }

                if (i % 100 == 0)
                {
                    EditorUtility.DisplayCancelableProgressBar("Search Helper Tool", "Find All Assets",
                        (float)i / objectContexts.Count);
                }
            }

            EditorUtility.ClearProgressBar();
            return results;
        }

        public List<ObjectContext> FindUnused()
        {
            var results = new List<ObjectContext>();
            var objectContexts = FindAllAssets().ToList();

            if (!objectContexts.Any())
            {
                return results;
            }

            var dict = new Dictionary<Object, List<ObjectContext>>();
            foreach (var objectContext in objectContexts)
            {
                if (dict.ContainsKey(objectContext.Object))
                {
                    Debug.Log("Error!");
                    continue;
                }

                dict[objectContext.Object] = new List<ObjectContext>(){objectContext};
            }

            EditorUtility.DisplayCancelableProgressBar("Search Helper Tool", "Find All Assets", 0f);
            for (var i = 0; i < objectContexts.Count; i++)
            {
                var objectContext = objectContexts[i];


                var dependencies = EditorUtility.CollectDependencies(new[] { objectContext.Object });
                foreach (var dependency in dependencies)
                {
                    if (dict.ContainsKey(dependency))
                    {
                        dict[dependency].Add(objectContext);
                    }
                }

                if (i % 100 == 0)
                {
                    EditorUtility.DisplayCancelableProgressBar("Search Helper Tool", "Find All Assets",
                        (float)i / objectContexts.Count);
                }
            }

            results = dict.Where(kv => kv.Value.Count == 1).Select(kv => kv.Value[0]).ToList();

            EditorUtility.ClearProgressBar();
            return results;
        }

        public Object FindObjectByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        public IEnumerable<ObjectContext> ToObjectContexts(Object[] objects, Object mainObject = null)
        {
            if (objects == null)
            {
                return null;
            }

            if (mainObject != null)
            {
                objects = objects.Where(value => value != mainObject).ToArray();
            }

            return objects.Select(element => new ObjectContext()
            {
                Object = element,
                Path = AssetDatabase.GetAssetPath(element)
            });
        }

        public ObjectContext FindDependencies(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var objectDependencies = ToObjectContexts(EditorUtility.CollectDependencies(new[] { obj }), obj);
            var dependencies = new Dependencies(objectDependencies);
            var path = AssetDatabase.GetAssetPath(obj);
            var guid = AssetDatabase.AssetPathToGUID(path);

            var objectContext = new ObjectContext()
            {
                Object = obj,
                Path = path,
                Guid = guid,
                Dependencies = dependencies
            };

            return objectContext;
        }

        public Dictionary<string, List<ObjectContext>> HashDictionary { get; set; }

        public Dictionary<string, List<ObjectContext>> FindDuplicates(string basePath)
        {
            var assets = FindAllAssets(basePath).ToList();
            var dict = new Dictionary<string, List<ObjectContext>>();

            var md5 = MD5.Create();
            foreach (var asset in assets)
                try
                {
                    var hashBytes = md5.ComputeHash(File.ReadAllBytes(asset.Path));
                    var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    if (dict.ContainsKey(hash))
                    {
                        dict[hash].Add(asset);
                    }
                    else
                    {
                        dict.Add(hash, new List<ObjectContext>() { asset });
                    }
                }
                catch
                {
                    // ignored
                }

            return dict.Where(kv => kv.Value.Count > 1).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void MergeAssets(List<ObjectContext> objectContexts)
        {
            if (objectContexts == null)
            {
                return;
            }

            if (objectContexts.Count < 2)
            {
                return;
            }

            AssetDatabase.StartAssetEditing();

            var baseObj = objectContexts[0];
            var baseGuid = AssetDatabase.GUIDFromAssetPath(baseObj.Path);
            if (baseGuid.Empty())
            {
                return;
            }

            for (var i = 1; i < objectContexts.Count; i++)
            {
                var removedObject = objectContexts[i];
                var usedByObjects = FindUsedBy(removedObject.Object);
                if (usedByObjects == null)
                {
                    continue;
                }

                foreach (var objectContext in usedByObjects)
                    FindAndReplaceInFile(new FileInfo(objectContext.Path), @"(?<=guid: )([0-9a-f]{32})", match =>
                    {
                        if (match.Groups[0].Captures[0].Value == removedObject.Guid)
                        {
                            return baseGuid.ToString().Replace("-", "");
                        }

                        return match.Groups[0].Captures[0].Value;
                    });

                AssetDatabase.DeleteAsset(removedObject.Path);
            }

            AssetDatabase.StopAssetEditing();
        }

        private static void FindAndReplaceInFile(FileInfo file, string pattern, MatchEvaluator match)
        {
            string buffer;
            using (var fs = file.OpenRead())
            using (var reader = new StreamReader(fs))
            {
                buffer = reader.ReadToEnd();
            }

            var regex = new Regex(pattern, RegexOptions.Multiline);
            buffer = regex.Replace(buffer, match);

            using (var fs = file.OpenWrite())
            using (var writer = new StreamWriter(fs))
            {
                writer.Write(buffer);
            }
        }

        public void SetCurrentObject(ObjectContext objectContext)
        {
            if (objectContext?.IsValid ?? false)
            {
                CurrentUsesObject = objectContext;
                SelectedUsesObject = objectContext.Object;
            }
        }

        public void SetCurrentObjects(List<ObjectContext> objectContexts, Object selectedObject)
        {
            if (selectedObject == null || objectContexts == null)
            {
                return;
            }

            SelectedUsesObject = selectedObject;
            CurrentUsedByObjects = objectContexts;
        }

        #region GUI

        public void OnGUI()
        {
            if (PanelNames?.Length > 0)
            {
                var panel = (Panel)GUILayout.SelectionGrid((int)CurrentPanel, PanelNames, PanelNames.Length);
                GUILayout.Space(10);
                DrawPanel(panel);
            }
        }

        private SearchHelperTool ChangePanel(Panel newPanel)
        {
            if (CurrentPanel != newPanel)
            {
                CurrentPanel = newPanel;
                ScrollViewPos = Vector2.zero;
            }

            return this;
        }

        private void DrawPanel(Panel panel)
        {
            ChangePanel(panel);

            GUIHelper.ScrollView(ScrollViewPos, () =>
            {
                switch (panel)
                {
                    case Panel.Uses:
                        DrawUsesPanel();
                        break;
                    case Panel.FindByGuid:
                        DrawFindByGuid();
                        break;
                    case Panel.UsedBy:
                        DrawUsedByPanel();
                        break;
                    case Panel.Duplicate:
                        DrawDuplicatePanel();
                        break;
                    case Panel.Unused:
                        DrawUnusedPanel();
                        break;
                    case Panel.UsesInBuild:
                        DrawBoilerplatePanel();
                        break;
                    case Panel.AssetsByType:
                        DrawBoilerplatePanel();
                        break;
                }
            });
        }

        #region Panels

        private void DrawUsesPanel()
        {
            DrawSelectedObjectField(selectedObject =>
            {
                if (GUILayout.Button("Find Dependencies"))
                {
                    SetCurrentObject(FindDependencies(selectedObject));
                }

                GUILayout.FlexibleSpace();
            }, _ => { DrawObjectContext(CurrentUsesObject, true); });
        }

        private void DrawFindByGuid()
        {
            GUIHelper.Horizontal(() =>
            {
                GUILayout.Label("GUID:");
                CurrentGuid = GUILayout.TextField(CurrentGuid, GUILayout.ExpandWidth(false), GUILayout.Width(250));

                GUIHelper.Enabled(!string.IsNullOrEmpty(CurrentGuid), () =>
                {
                    GUIHelper.Button("Find by GUID", () =>
                    {
                        CurrentGuidObject = FindDependencies(FindObjectByGuid(CurrentGuid));
                        IsGuidObjectFound = CurrentGuidObject != null;
                    });

                    GUIHelper.Button("Clean", () =>
                    {
                        CurrentGuidObject = null;
                        IsGuidObjectFound = null;
                    });
                });
                GUILayout.FlexibleSpace();
            });

            if (!IsGuidObjectFound.HasValue)
            {
                if (!GUID.TryParse(CurrentGuid, out _))
                {
                    GUIHelper.Color(WarningColor, () => GUILayout.Label($"The guid [{CurrentGuid}] is invalid."));
                }
                else
                {
                    GUILayout.Label($"The guid [{CurrentGuid}] is valid.");
                }
            }
            else
            {
                if (IsGuidObjectFound.Value)
                {
                    DrawObjectContext(CurrentGuidObject);
                }
                else
                {
                    GUIHelper.Color(ErrorColor, () => GUILayout.Label($"The object with [{CurrentGuid}] isn't found."));
                }
            }
        }

        private void DrawUsedByPanel()
        {
            DrawSelectedObjectField(selectedObject =>
            {
                GUIHelper.Button("Find",
                    () => { CurrentUsedByObjects = FindUsedBy(selectedObject, ShouldFindDependencies); });
                GUIHelper.Button("Clean", () =>
                {
                    SelectedUsesObject = null;
                    CurrentUsedByObjects = null;
                });
                GUIHelper.Toggle("Ignore Editor folders", ShouldEditorAssetsBeIgnored,
                    value => ShouldEditorAssetsBeIgnored = value);
                GUIHelper.Toggle("Should Find Dependencies", ShouldFindDependencies,
                    value => ShouldFindDependencies = value);
                GUILayout.FlexibleSpace();
            }, selectedObject =>
            {
                if (CurrentUsedByObjects == null)
                {
                    return;
                }

                if (CurrentUsedByObjects.Count == 0 && SelectedUsesObject != null)
                {
                    GUIHelper.Color(ErrorColor, () => GUILayout.Label("No objects found."));
                }

                foreach (var objectContext in CurrentUsedByObjects)
                    DrawObjectContext(objectContext);
            });
        }

        private void DrawDuplicatePanel()
        {
            DrawSelectedObjectField( selectedObject =>
            {
                string path = null;
                var findDuplicatedButtonText = "Find Duplicated";
                if (selectedObject != null)
                {
                    path = AssetDatabase.GetAssetPath(selectedObject);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        findDuplicatedButtonText = "Find Duplicates in Folder";
                    }
                }

                GUIHelper.Button(findDuplicatedButtonText, () => { HashDictionary = FindDuplicates(path); });

                GUIHelper.Button("Clear", () => { HashDictionary = null; });

                GUILayout.FlexibleSpace();
            });


            GUILayout.Space(10);

            if (HashDictionary != null)
            {
                foreach (var (hash, objectContexts) in HashDictionary)
                    GUIHelper.Vertical("Box", () =>
                    {
                        GUIHelper.Horizontal("Box", () =>
                        {
                            GUILayout.Label($"Hash: {hash}");
                            GUIHelper.Button("Merge into one", () => { MergeAssets(objectContexts); });
                            GUILayout.FlexibleSpace();
                        });

                        foreach (var objectContext in objectContexts)
                            DrawObjectContext(objectContext);
                    });
            }
        }

        private void DrawUnusedPanel()
        {
            GUIHelper.Horizontal(() =>
            {
                GUIHelper.Button("Find Unused", () =>
                {
                    CurrentUnusedObjects = FindUnused();

                });

                GUIHelper.Button("Clear", () => { CurrentUnusedObjects = null; });
                GUILayout.FlexibleSpace();
            });

            GUILayout.Space(10);

            if (CurrentUnusedObjects != null)
            {
                foreach (var unusedObject in CurrentUnusedObjects)
                {

                    GUIHelper.Vertical("Box", () => { DrawObjectContext(unusedObject); });
                }
            }
        }

        private void DrawBoilerplatePanel()
        {
            GUILayout.Label("The functionality is under development");
        }

        #endregion

        private void DrawSelectedObjectField(Action<Object> horizontalElements = null,
            Action<Object> verticalElements = null)
        {
            GUIHelper.Horizontal(() =>
            {
                SelectedUsesObject = EditorGUILayout.ObjectField(SelectedUsesObject, typeof(Object), true);

                if (CurrentUsesObject != null && SelectedUsesObject != null)
                {
                    if (CurrentUsesObject.Object != SelectedUsesObject)
                    {
                        CurrentUsesObject = null;
                    }
                }

                GUIHelper.Enabled(SelectedUsesObject != null,
                    () => { horizontalElements?.Invoke(SelectedUsesObject); });
            });

            GUIHelper.Vertical(() => { verticalElements?.Invoke(SelectedUsesObject); });
        }

        private void DrawObjectContext(ObjectContext objectContext, bool? expanded = null)
        {
            if (objectContext == null || !objectContext.IsValid || objectContext.Dependencies == null)
            {
                return;
            }

            if (expanded.HasValue)
            {
                objectContext.Dependencies.IsExpanded = expanded.Value;
            }

            GUIHelper.Vertical(() =>
            {
                GUIHelper.Horizontal("Box", () =>
                {
                    objectContext.Dependencies.IsExpanded =
                        EditorGUILayout.BeginFoldoutHeaderGroup(objectContext.Dependencies.IsExpanded,
                            objectContext.Path);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.ObjectField(objectContext.Object, typeof(Object), objectContext.Object);
                    GUILayout.Label("Guid: ");
                    GUILayout.TextArea(objectContext.Guid, GUILayout.Width(250));
                });

                if (objectContext.Dependencies.IsExpanded)
                {
                    GUIHelper.Horizontal(() =>
                    {
                        if (objectContext.Dependencies.Count == 0)
                        {
                            GUIHelper.Color(ErrorColor,
                                () => GUILayout.Label($"Dependencies: {objectContext.Dependencies.Count}"));
                        }
                        else
                        {
                            GUILayout.Label($"Dependencies: {objectContext.Dependencies.Count}");
                            GUILayout.FlexibleSpace();
                            GUILayout.Label("Filter by path:");
                            objectContext.Dependencies.Filter = GUILayout.TextField(objectContext.Dependencies.Filter,
                                GUILayout.ExpandWidth(false), GUILayout.MinWidth(500));
                        }
                    });
                }

                if (objectContext.Dependencies.IsExpanded)
                {
                    foreach (var dependency in objectContext.Dependencies)
                    {
                        var filterString = objectContext.Dependencies.Filter;
                        if (!string.IsNullOrEmpty(filterString) && !dependency.Path.Contains(filterString))
                        {
                            continue;
                        }

                        GUIHelper.Horizontal(() =>
                        {
                            EditorGUILayout.ObjectField(dependency.Object, typeof(Object), false,
                                GUILayout.ExpandWidth(false), GUILayout.MinWidth(500));
                            GUILayout.TextArea(dependency.Path);
                        });
                    }
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
            });
        }

        #endregion
    }
}