using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Search.Helper.Runtime.Helpers;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Search.Helper.Editor.Tools
{
    public class ReferenceTool : EditorWindow
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

        private ObjectContext CurrentUsesObject { get; set; }
        private Object SelectedUsesObject { get; set; }
        public string CurrentGuid { get; set; }
        public ObjectContext CurrentGuidObject { get; set; }
        public bool? IsGuidObjectFound { get; set; }

        private Panel CurrentPanel { get; set; } = Panel.Uses;
        private Vector2 ScrollViewPos { get; set; } = Vector2.zero;

        private string[] PanelNames { get; set; }
        private Color ErrorColor => Color.red;
        private Color WarningColor => Color.yellow;

        [MenuItem(WindowMenuItemName)]
        public static ReferenceTool OpenWindow()
        {
            return GetWindow<ReferenceTool>(WindowTitle);
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
            window?.SetCurrentObject(window?.FindDependencies(Selection.activeObject));
        }

        [MenuItem(ContextMenuItemFindUsesName, true)]
        [MenuItem(ContextMenuFindUsedByItemName, true)]
        public static bool ValidateActiveSelectedObject()
        {
            return Selection.activeObject;
        }

        public ReferenceTool()
        {
            PanelNames = Enum.GetNames(typeof(Panel)).Select(element => element.AddSpacesBeforeUppercase()).ToArray();
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

        public ObjectContext FindDependencies(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var objectDependencies = EditorUtility.CollectDependencies(new[] { obj })
                                                  .Where(element => element != obj)
                                                  .Select(element => new ObjectContext()
                                                  {
                                                      Object = element,
                                                      Path = AssetDatabase.GetAssetPath(element),
                                                      Dependencies = null
                                                  });

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

        public void SetCurrentObject(ObjectContext objectContext)
        {
            if (objectContext?.IsValid ?? false)
            {
                CurrentUsesObject = objectContext;
                SelectedUsesObject = objectContext.Object;
            }
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

        private ReferenceTool ChangePanel(Panel newPanel)
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
                        DrawBoilerplatePanel();
                        break;
                    case Panel.Unused:
                        DrawBoilerplatePanel();
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
            DrawSelectedObjectField(() => SelectedUsesObject, selectedObject =>
            {
                SelectedUsesObject = selectedObject;
                if (CurrentUsesObject == null || SelectedUsesObject == null)
                {
                    return;
                }

                if (CurrentUsesObject.Object != SelectedUsesObject)
                {
                    CurrentUsesObject = null;
                }
            }, selectedObject =>
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
        }

        private void DrawBoilerplatePanel()
        {
            GUILayout.Label("The functionality is under development");
        }

        #endregion

        private void DrawSelectedObjectField(Func<Object> getter, Action<Object> setter,
            Action<Object> horizontalElements = null, Action<Object> verticalElements = null)
        {
            GUIHelper.Horizontal(() =>
            {
                var newValue = EditorGUILayout.ObjectField(getter(), typeof(Object), true);
                setter?.Invoke(newValue);

                GUIHelper.Enabled(getter() != null, () => { horizontalElements?.Invoke(getter()); });
            });

            GUIHelper.Vertical(() => { verticalElements?.Invoke(getter()); });
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
                    GUILayout.TextArea(objectContext.Guid, GUILayout.ExpandWidth(false));
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