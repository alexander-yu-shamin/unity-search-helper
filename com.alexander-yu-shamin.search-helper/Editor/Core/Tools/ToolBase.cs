using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.Data;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor
{
    public abstract class ToolBase
    {
        protected enum SortVariant
        {
            None = 0,
            ByName,
            ByPath,
            Natural
        }

        protected enum FilterVariant
        {
            Path,
            Name,
            Type
        }

        protected virtual bool IsScopeRulesSupported { get; set; } = false;
        protected virtual bool IsIgnoredFilesSupported { get; set; } = true;
        protected virtual bool IsSortingSupported { get; set; } = true;
        protected virtual bool ShouldMainObjectsBeSorted { get; set; } = false;
        protected virtual bool IsShowFoldersSupported { get; set; } = true;
        protected virtual bool DrawObjectWithEmptyDependencies { get; set; } = false;
        protected virtual string EmptyObjectContextText { get; set; } = "The object doesn't have any dependencies.";

        protected const float RowHeight = 20.0f;
        protected const float RowPadding = 2f;

        protected const float ContentHeight = RowHeight;
        protected const float ContentPadding = RowPadding;
        protected const float ContentHeightWithPadding = ContentHeight + ContentPadding;

        protected const float HeaderHeight = ContentHeight;
        protected const float HeaderPadding = 6.0f;
        protected const float HeaderHeightWithPadding = ContentHeight + HeaderPadding * 2;

        protected const float HorizontalIndent = 15.0f;
        protected const float FirstElementIndent = 4.0f;
        protected const float ScrollBarWidth = 16.0f;
        protected const float NoScrollBarWidth = 4.0f;
        protected const float GuidTextAreaWidth = 275.0f;
        protected const float ExtraHeightToPreventBlinking = ContentHeightWithPadding * 5;
        protected const float BottomIndent = ContentHeightWithPadding * 3;
        protected const float SelectedObjectWidth = HeaderHeight + 250.0f + HorizontalIndent / 2;

        protected static readonly Color BoxColor = new Color(0.0f, 0.0f, 0.0f, 0.2f);
        protected static readonly Color EmptyColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        protected const string FolderIconName = "d_Folder Icon";
        protected const string InspectorIconName = "d_UnityEditor.InspectorWindow";
        protected const string HierarchyIconName = "d_UnityEditor.SceneHierarchyWindow";

        protected const string SceneHierarchySearchReferenceFormat = "ref:{0}";

        protected static readonly Color ImportantColor = Color.yellow;
        protected static readonly Color WarningColor = Color.yellow;
        protected static readonly Color ErrorColor = Color.red;

        private Vector2 ScrollViewPosition { get; set; }
        protected SortVariant CurrentSortVariant { get; set; } = SortVariant.None;
        protected FilterVariant CurrentFilterVariant { get; set; } = FilterVariant.Path;
        private string FilterString { get; set; }
        private bool IsFoldersShown { get; set; } = false;

        private DataDescription<SearchHelperIgnoreRule> ChosenPattern { get; set; }
        private List<DataDescription<SearchHelperIgnoreRule>> Patterns { get; set; }
        private List<Regex> RegexIgnoredPaths { get; set; }
        private List<Regex> RegexIgnoredNames { get; set; }
        private List<Regex> RegexIgnoredTypes { get; set; }

        protected abstract IEnumerable<ObjectContext> Data { get; }
        public bool IsGlobalScope { get; set; } = true;

        public abstract void Draw(Rect windowRect);

        public abstract void Run(Object selectedObject);

        public virtual void GetDataFromAnotherTool(IEnumerable<ObjectContext> contexts)
        {
        }

        public virtual void GetDataFromAnotherTool(ObjectContext context)
        {
        }

        public virtual void AssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
        }

        protected bool Sort(SortVariant sortVariant)
        {
            if (Data.IsNullOrEmpty())
            {
                return false;
            }

            if (sortVariant == SortVariant.None)
            {
                return true;
            }

            if (ShouldMainObjectsBeSorted)
            {
                Sort(Data, sortVariant);
            }

            foreach (var context in Data.Where(context => !context.Dependencies.IsNullOrEmpty()))
            {
                context.Dependencies = Sort(context.Dependencies, sortVariant).ToList();
            }

            return true;
        }

        protected void UpdateData(IEnumerable<ObjectContext> contexts = null)
        {
            contexts ??= Data;

            if (contexts.IsNullOrEmpty())
            {
                return;
            }

            foreach (var context in contexts)
            {
                context.ShouldBeShown = ShouldBeShown(context);
                if (context.Dependencies.IsNullOrEmpty())
                {
                    continue;
                }

                foreach (var dependency in context.Dependencies)
                {
                    dependency.ShouldBeShown = ShouldBeShown(dependency);
                }
            }

            Sort(CurrentSortVariant);
        }

        protected void DrawVirtualScroll(Rect windowRect, List<ObjectContext> contexts, bool drawDependencies = true)
        {
            if (contexts.IsNullOrEmpty())
            {
                return;
            }

            GUILayout.Space(HeaderPadding);

            ScrollViewPosition = EditorGUILayout.BeginScrollView(ScrollViewPosition, GUILayout.Height(windowRect.height - BottomIndent));

            var totalHeight = CalculateDisplayedHeight(contexts);
            var fullRect = GUILayoutUtility.GetRect(0, totalHeight);

            var x = fullRect.x;
            var y = ScrollViewPosition.y;
            var currentY = 0.0f;
            var drawnHeight = 0.0f;

            var displayRect = new Rect(0, 0,
                totalHeight > windowRect.height
                    ? windowRect.width - ScrollBarWidth
                    : windowRect.width - NoScrollBarWidth, windowRect.height + ExtraHeightToPreventBlinking);

            foreach (var ctx in contexts)
            {
                if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect, 
                        () => TryDrawObjectHeader(ref x, ref y, displayRect.width, ctx),
                        () => CalculateHeaderHeight(ctx)))
                {
                    break;
                }

                if (!drawDependencies)
                {
                    continue;
                }

                if (ctx.Dependencies.IsNullOrEmpty())
                {
                    if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect, 
                            () => TryDrawEmptyContent(ref x, ref y, displayRect.width, ctx),
                            () => CalculateEmptyHeight(ctx)))
                    {
                        break;
                    }
                }
                else
                {
                    foreach (var dependency in ctx.Dependencies)
                    {
                        if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect, 
                                () => TryDrawContent(ref x, ref y, displayRect.width, dependency, ctx),
                                () => CalculateDependencyHeight(dependency, ctx)))
                        {
                            break;
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private bool IsVisible(float accumulatedHeight, Vector2 scrollViewPosition, Rect visibleRect,
            out bool beforeVisibleRect, out bool afterVisibleRect)
        {
            beforeVisibleRect = accumulatedHeight < scrollViewPosition.y;
            afterVisibleRect = accumulatedHeight >= scrollViewPosition.y + visibleRect.height;
            return !beforeVisibleRect && !afterVisibleRect;
        }

        private float CalculateDisplayedHeight(List<ObjectContext> contexts)
        {
            return contexts.Sum(ctx => CalculateHeaderHeight(ctx) + CalculateDependenciesHeight(ctx));
        }

        private bool TryDraw(ref float currentY, Vector2 scrollViewPosition, ref float drawnHeight, Rect windowRect, Func<float> tryDraw, Func<float> calculateHeight)
        {
            if (IsVisible(currentY, scrollViewPosition, windowRect, out _, out var afterVisibleRect))
            {
                drawnHeight += tryDraw?.Invoke() ?? 0.0f;
            }

            currentY += calculateHeight?.Invoke() ?? 0.0f;
            return !afterVisibleRect;
        }

        private float CalculateDependenciesHeight(ObjectContext context)
        {
            if (context.Dependencies.IsNullOrEmpty())
            {
                return CalculateEmptyHeight(context);
            }
            else
            {
                return context.Dependencies.Sum(dependency => CalculateDependencyHeight(dependency, context));
            }
        }

        private float CalculateHeaderHeight(ObjectContext context)
        {
            if (context.IsFolder && !IsFoldersShown)
            {
                return 0.0f;
            }

            if (IsIgnoredFilesSupported && !context.ShouldBeShown)
            {
                return 0.0f;
            }

            if (!DrawObjectWithEmptyDependencies && !context.Dependencies.Any(dependency => dependency.ShouldBeShown))
            {
                return 0.0f;
            }

            return HeaderHeightWithPadding;
        }

        private float TryDrawObjectHeader(ref float x, ref float y, float width, ObjectContext context)
        {
            if (context.IsFolder && !IsFoldersShown)
            {
                return 0.0f;
            }

            if (IsIgnoredFilesSupported && !context.ShouldBeShown)
            {
                return 0.0f;
            }

            if (!DrawObjectWithEmptyDependencies && !context.Dependencies.Any(dependency => dependency.ShouldBeShown))
            {
                return 0.0f;
            }

            var result = DrawObjectHeader(new Rect(x, y, width, HeaderHeightWithPadding), context);
            y += result;
            return result;
        }

        private float DrawObjectHeader(Rect rect, ObjectContext context)
        {
            var x = rect.x + FirstElementIndent;
            var y = rect.y;
            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, HeaderPadding), EmptyColor);
            y += HeaderPadding;

            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, HeaderHeight + HeaderPadding), BoxColor);
            y += HeaderPadding / 2;

            var elementWidth = HeaderHeight;
            if (GUI.Button(new Rect(x, y - 1, elementWidth, HeaderHeight),
                    EditorGUIUtility.IconContent(FolderIconName)))
            {
                OpenInDefaultFileBrowser(context);
            }

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = 250.0f;
            var objectFieldRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
            EditorGUI.ObjectField(objectFieldRect, context.Object, typeof(Object), context.Object);

            DrawContextMenu(context, objectFieldRect);

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = EditorStyles.foldoutHeader.CalcSize(new GUIContent(context.Path)).x;
            context.IsExpanded = EditorGUI.BeginFoldoutHeaderGroup(new Rect(x, y, elementWidth, HeaderHeight),
                context.IsExpanded, context.Path);
            EditorGUI.EndFoldoutHeaderGroup();

            x += elementWidth + HorizontalIndent;

            var leftWidth = rect.width - x;
            var neededWidthForDependency = GuidTextAreaWidth + 40.0f + 50.0f + 100.0f;
            var neededWidthForGuid = GuidTextAreaWidth + 40.0f;

            if (leftWidth > neededWidthForGuid)
            {
                elementWidth = GuidTextAreaWidth;
                x = rect.width - elementWidth;
                EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight), context.Guid);

                elementWidth = 40.0f;
                x -= elementWidth;
                EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), "GUID:");
            }

            if (leftWidth > neededWidthForDependency)
            {
                elementWidth = 50.0f;
                x -= elementWidth + HorizontalIndent;
                EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight),
                    context.Dependencies?.Count.ToString());

                elementWidth = 100.0f;
                x -= elementWidth;
                EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), "Dependencies:");
            }

            return HeaderHeightWithPadding;
        }

        private float CalculateEmptyHeight(ObjectContext mainContext)
        {
            if (mainContext.IsFolder)
            {
                return 0.0f;
            }

            if (!mainContext.IsExpanded)
            {
                return 0.0f;
            }

            if (IsIgnoredFilesSupported && !mainContext.ShouldBeShown)
            {
                return 0.0f;
            }

            return ContentHeight;
        }

        private float TryDrawEmptyContent(ref float x, ref float y, float width, ObjectContext mainContext)
        {
            if (mainContext.IsFolder)
            {
                return 0.0f;
            }

            if (!mainContext.IsExpanded)
            {
                return 0.0f;
            }

            if (IsIgnoredFilesSupported && !mainContext.ShouldBeShown)
            {
                return 0.0f;
            }

            var result = DrawEmptyContent(new Rect(x, y, width, ContentHeightWithPadding), EmptyObjectContextText);
            y += result;
            return result;
        }

        private float DrawEmptyContent(Rect rect, string text)
        {
            EditorGUI.DrawRect(rect, BoxColor);
            EGuiKit.Color(ErrorColor, () =>
            {
                EditorGUI.LabelField(new Rect(rect.x + FirstElementIndent, rect.y, rect.width, rect.height), text);
            });
            return ContentHeightWithPadding;
        }

        private float CalculateDependencyHeight(ObjectContext dependency, ObjectContext mainContext)
        {
            if (!mainContext.IsExpanded)
            {
                return 0.0f;
            }

            if (!dependency.ShouldBeShown)
            {
                return 0.0f;
            }

            return ContentHeightWithPadding;
        }

        private float TryDrawContent(ref float x, ref float y, float width, ObjectContext context, ObjectContext mainContext)
        {
            if (!mainContext.IsExpanded)
            {
                return 0.0f;
            }

            if (!context.ShouldBeShown)
            {
                return 0.0f;
            }

            var result = DrawContent(new Rect(x, y, width, ContentHeightWithPadding), context);
            y += result;
            return result;
        }

        private float DrawContent(Rect rect, ObjectContext context)
        {
            EditorGUI.DrawRect(rect, BoxColor);

            var elementWidth = 500.0f;
            var x = rect.x + FirstElementIndent;
            var objectFieldRect = new Rect(x, rect.y, elementWidth, ContentHeight);
            EditorGUI.ObjectField(objectFieldRect, context.Object, typeof(Object),
                context.Object);
            x += elementWidth + HorizontalIndent / 2;

            DrawContextMenu(context, objectFieldRect);

            elementWidth = ContentHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, HeaderHeight), EditorGUIUtility.IconContent(HierarchyIconName)))
            {
                FindInHierarchyWindow(context);
            }
            x += elementWidth + HorizontalIndent / 2;

            elementWidth = ContentHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, HeaderHeight), EditorGUIUtility.IconContent(InspectorIconName)))
            {
                OpenProperty(context);
            }
            x += elementWidth + HorizontalIndent / 2;

            elementWidth = 40.0f;
            EditorGUI.LabelField(new Rect(x, rect.y, elementWidth, ContentHeight), "GUID:");
            x += elementWidth;

            elementWidth = GuidTextAreaWidth;
            EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, ContentHeight), context.Guid);
            x += elementWidth + HorizontalIndent / 2;

            elementWidth = ContentHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, HeaderHeight), EditorGUIUtility.IconContent(FolderIconName)))
            {
                if (!string.IsNullOrEmpty(context.Path))
                {
                    EditorUtility.RevealInFinder(context.Path);
                }
            }
            x += elementWidth + HorizontalIndent / 2;

            elementWidth = 40.0f;
            EditorGUI.LabelField(new Rect(x, rect.y, elementWidth, ContentHeight), "Path:");
            x += elementWidth;

            elementWidth = rect.width - x;
            EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, ContentHeight), context.Path);

            return ContentHeightWithPadding;
        }

        private void DrawContextMenu(ObjectContext context, Rect objectFieldRect)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1 && objectFieldRect.Contains(e.mousePosition))
            {
                ShowContextMenu(context);
            }
        }

        protected void DrawHeaderControls()
        {
            EGuiKit.Horizontal(() =>
            {
                if (IsShowFoldersSupported)
                {
                    IsFoldersShown = EditorGUILayout.ToggleLeft("Show Folders", IsFoldersShown, GUILayout.Width(100));
                    EGuiKit.Space(HorizontalIndent);
                }

                DrawScopeRules();
                DrawIgnoringRules();
                DrawSortingRules();
                DrawFilterRules();
            });
        }

        private void DrawScopeRules()
        {
            if (!IsScopeRulesSupported)
            {
                return;
            }

            EGuiKit.Button(IsGlobalScope ? "Global" : "Local", () =>
            {
                IsGlobalScope = !IsGlobalScope;
            });
        }

        private void DrawFilterRules()
        {
            var content = new GUIContent($"{CurrentFilterVariant} contains:");
            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();
                foreach (FilterVariant variant in Enum.GetValues(typeof(FilterVariant)))
                {
                    menu.AddItem(new GUIContent(variant.ToString()), CurrentFilterVariant == variant, () =>
                    {
                        UpdateFilterVariant(variant);
                    });
                }

                menu.ShowAsContext();
            }

            var newFilterString = EditorGUILayout.TextArea(FilterString, GUILayout.Width(250));
            if (FilterString != newFilterString)
            {
                FilterString = newFilterString;
                UpdateData();
            }

            EGuiKit.Space(HorizontalIndent);
        }

        private void DrawSortingRules()
        {
            if (!IsSortingSupported)
            {
                return;
            }

            EGuiKit.Label("Sorting:");
            var newSortVariant = (SortVariant)GUILayout.Toolbar((int)CurrentSortVariant, System.Enum.GetNames(typeof(SortVariant)));
            if (CurrentSortVariant != newSortVariant)
            {
                if (Sort(newSortVariant))
                {
                    CurrentSortVariant = newSortVariant;
                }
            }
            EGuiKit.Space(HorizontalIndent);
        }

        private void DrawIgnoringRules()
        {
            if (!IsIgnoredFilesSupported)
            {
                return;
            }

            UpdatePatternsIfNeeded();

            var content = new GUIContent(ChosenPattern?.Name ?? "No Ignore Rule");
            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();
                if (Patterns != null)
                {
                    foreach (var pattern in Patterns)
                    {
                        menu.AddItem(new GUIContent(pattern.Name), ChosenPattern?.Name == pattern.Name, () =>
                        {
                            UpdatePattern(pattern);
                        });
                    }
                }

                menu.AddItem(new GUIContent("Load more from disk"), false, () =>
                {
                    UpdatePatternsIfNeeded(true);
                });

                menu.AddItem(new GUIContent("Remove Pattern"), false, () =>
                {
                    UpdatePattern(null);
                });

                menu.ShowAsContext();
            }
        }

        private bool ShouldBeShown(ObjectContext objectContext)
        {
            if (string.IsNullOrEmpty(objectContext.Path))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(FilterString))
            {
                switch (CurrentFilterVariant)
                {
                    case FilterVariant.Name:
                        if (!objectContext.Object.name.Contains(FilterString))
                        {
                            return false;
                        }

                        break;
                    case FilterVariant.Path:
                        if (!objectContext.Path.Contains(FilterString))
                        {
                            return false;
                        }
                        break;
                    case FilterVariant.Type:
                        if (!objectContext.Object.GetType().FullName.Contains(FilterString))
                        {
                            return false;
                        }
                        break;
                }
            }

            if (IsIgnoredFilesSupported)
            {
                if (RegexIgnoredPaths?.Any(regex => regex.IsMatch(objectContext.Path)) ?? false)
                {
                    return false;
                }

                if (RegexIgnoredNames?.Any(regex => regex.IsMatch(objectContext.Object.name)) ?? false)
                {
                    return false;
                }

                if (RegexIgnoredTypes?.Any(regex => regex.IsMatch(objectContext.Object.GetType().FullName)) ?? false)
                {
                    return false;
                }
            }

            return true;
        }

        protected IEnumerable<ObjectContext> Sort(IEnumerable<ObjectContext> objectContexts, SortVariant sortVariant)
        {
            switch (sortVariant)
            {
                case SortVariant.ByName:
                    return objectContexts.OrderBy(el => el.Object.name);
                case SortVariant.ByPath:
                    return objectContexts.OrderBy(el => el.Path);
                case SortVariant.Natural:
                    return objectContexts.OrderBy(el => el.Object.name, Comparer<string>.Create(EditorUtility.NaturalCompare));
                case SortVariant.None:
                default:
                    return objectContexts;
            }
        }

        protected IEnumerable<Object> FolderOrFile(Object obj)
        {
            var path = PathFromObject(obj);
            return string.IsNullOrEmpty(FolderPathFromObject(obj)) ? obj.AsIEnumerable() : SearchHelperService.FindAssetObjects(path);
        }

        private string PathFromObject(Object obj)
        {
            return AssetDatabase.GetAssetPath(obj);
        }

        protected string FolderPathFromObject(Object obj)
        {
            var path = PathFromObject(obj);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.IsValidFolder(path) ? path : null;
        }

        private void UpdateFilterVariant(FilterVariant newFilterVariant)
        {
            CurrentFilterVariant = newFilterVariant;
            UpdateData();
        }

        private void UpdatePattern(DataDescription<SearchHelperIgnoreRule> newPattern)
        {
            if (newPattern == null)
            {
                RegexIgnoredPaths = null;
                RegexIgnoredNames = null;
                RegexIgnoredTypes = null;
                if (ChosenPattern != null)
                {
                    ChosenPattern.Data.OnDataChanged -= OnPatterChanged;
                    ChosenPattern = null;
                }
            }
            else
            {
                if (ChosenPattern != null)
                {
                    ChosenPattern.Data.OnDataChanged -= OnPatterChanged;
                }

                ChosenPattern = newPattern;
                ChosenPattern.Data.OnDataChanged += OnPatterChanged;

                RegexIgnoredPaths = new List<Regex>(ChosenPattern.Data.IgnoredPaths.Count);
                foreach (var ignoredPath in ChosenPattern.Data.IgnoredPaths)
                {
                    if (!string.IsNullOrEmpty(ignoredPath))
                    {
                        RegexIgnoredPaths.Add(new Regex(ignoredPath, RegexOptions.Compiled));
                    }
                }

                RegexIgnoredNames = new List<Regex>(ChosenPattern.Data.IgnoredNames.Count);
                foreach (var ignoredName in ChosenPattern.Data.IgnoredNames)
                {
                    if (!string.IsNullOrEmpty(ignoredName))
                    {
                        RegexIgnoredNames.Add(new Regex(ignoredName, RegexOptions.Compiled));
                    }
                }

                RegexIgnoredTypes = new List<Regex>(ChosenPattern.Data.IgnoredTypes.Count);
                foreach (var ignoredType in ChosenPattern.Data.IgnoredTypes)
                {
                    if (!string.IsNullOrEmpty(ignoredType))
                    {
                        RegexIgnoredTypes.Add(new Regex(ignoredType, RegexOptions.Compiled));
                    }
                }
            }

            UpdateData();
        }

        private void OnPatterChanged()
        {
            UpdatePattern(ChosenPattern);
        }

        private void UpdatePatternsIfNeeded(bool force = false)
        {
            var shouldBeUpdated = Patterns == null;

            if (shouldBeUpdated || force)
            {
                Patterns = SearchHelperDataSource.GetAllUnusedPatterns();

                foreach (var pattern in Patterns)
                {
                    if (pattern.Path.StartsWith("Packages"))
                    {
                        pattern.Name = "Default: " + Path.GetFileName(Path.GetDirectoryName(pattern.Path)) + "/" + Path.GetFileNameWithoutExtension(pattern.Path);
                        continue;
                    }

                    pattern.Name = Path.GetFileName(Path.GetDirectoryName(pattern.Path)) + "/" + Path.GetFileNameWithoutExtension(pattern.Path);
                }
            }
        }

        private void ShowContextMenu(ObjectContext context)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Open Folder"), false, () => { OpenInDefaultFileBrowser(context); });
            menu.AddItem(new GUIContent("Find In Project"), false, () => { FindInProject(context); });
            menu.AddItem(new GUIContent("Find In Scene"), false, () => { FindInHierarchyWindow(context); });
            menu.AddItem(new GUIContent("Open Property"), false, () => { OpenProperty(context); });
            menu.AddItem(new GUIContent("Copy/Path"), false, () => { CopyToClipboard(context.Path); });
            menu.AddItem(new GUIContent("Copy/GUID"), false, () => { CopyToClipboard(context.Guid); });
            menu.AddItem(new GUIContent("Copy/Type"), false, () => { CopyToClipboard(context.Object.GetType().FullName); });
            if (!context.Dependencies.IsNullOrEmpty())
            {
                menu.AddItem(new GUIContent("Select All"), false, () => { SelectAll(context); });
                menu.AddItem(new GUIContent("Select Dependencies"), false, () => { SelectDependencies(context); });
                menu.AddItem(new GUIContent("Copy/Dependency paths"), false,
                    () => { CopyToClipboard(string.Join(", ", context.Dependencies.Select(element => element.Path))); });
            }

            AddContextMenu(menu, context);

            menu.ShowAsContext();
        }

        protected virtual void AddContextMenu(GenericMenu menu, ObjectContext context)
        {
        }

        private void SelectDependencies(ObjectContext context)
        {
            if (context == null)
            {
                return;
            }

            if (context.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            Selection.objects = context.Dependencies.Select(el => el.Object).ToArray();
        }

        private void SelectAll(ObjectContext context)
        {
            if (context == null)
            {
                return;
            }

            if (context.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            Selection.objects = new[] { context.Object }
                                .Concat(context.Dependencies.Select(el => el.Object))
                                .ToArray();
        }

        private void FindInHierarchyWindow(ObjectContext context)
        {
            if (context?.Path == null)
            {
                return;
            }

            var sceneHierarchyType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            var window = EditorWindow.GetWindow(sceneHierarchyType);
            var method = sceneHierarchyType.GetMethod("SetSearchFilter", BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
            {
                Debug.LogError("Method SetSearchFilter isn't found.");
                return;
            }

            method.Invoke(window, new object[]
            {
                string.Format(SceneHierarchySearchReferenceFormat, context.Path),
                0, // SearchableEditorWindow.SearchMode.All
                false, // setAll
                false // delayed
            });
        }

        private void OpenInDefaultFileBrowser(ObjectContext context)
        {
            if (!string.IsNullOrEmpty(context?.Path))
            {
                EditorUtility.RevealInFinder(context.Path);
            }
        }

        private void FindInProject(ObjectContext context)
        {
            if (context?.Object != null)
            {
                EditorGUIUtility.PingObject(context.Object);
            }
        }

        private void OpenProperty(ObjectContext context)
        {
            if (context?.Object != null)
            {
                EditorUtility.OpenPropertyEditor(context.Object);
            }
        }

        protected void CopyToClipboard(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
        }
    }
}
