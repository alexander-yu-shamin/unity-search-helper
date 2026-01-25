using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SearchHelper.Editor.Core;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor
{
    public abstract class ToolBase
    {
        protected class Model
        {
            public bool DrawDependencies { get; set; } = true;
            public bool DrawObjectWithEmptyDependencies { get; set; } = true;
            public bool DrawMergeButtons { get; set; }
            public bool DrawEmptyDependency { get; set; } = true;
            public bool DrawState { get; set; } = true;
            public bool DrawEmptyFolder { get; set; } = true;

            public Func<ObjectContext, (string, Color)?> GetState { get; set; }
            public Func<ObjectContext, Color> GetObjectFieldColor { get; set; }
            public Action<ObjectContext> OnSelectedButtonPressed { get; set; }
            public Action<ObjectContext> OnRemoveButtonPressed { get; set; }
            public Action<ObjectContext> OnComparandButtonPressed { get; set; }
            public Action<ObjectContext> OnDiffButtonPressed { get; set; }
        }

        #region Settings
        protected virtual bool IsScopeRulesSupported { get; set; } = false;
        protected virtual bool IsFilterRuleSupported { get; set; } = true;
        protected virtual bool IsFilterStringSupported { get; set; } = true;
        protected virtual bool IsSortingSupported { get; set; } = true;

        protected virtual bool ShouldMainObjectsBeSorted { get; set; } = false;
        protected virtual bool IsShowingFoldersSupported { get; set; } = true;
        protected virtual bool IsSizeShowingSupported { get; set; } = false;
        protected virtual bool IsSettingsButtonEnabled { get; set; } = true;
        protected virtual bool IsEmptyDependencyShown { get; set; } = true;
        protected virtual bool IsCacheUsed { get; set; } = true;
        protected virtual string EmptyObjectContextText { get; set; } = "The object doesn't have any dependencies.";
        #endregion

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
        protected const float StateTextAreaWidth = 150.0f;
        protected const float ExtraHeightToPreventBlinking = ContentHeightWithPadding * 5;
        protected const float BottomIndent = ContentHeightWithPadding * 3;
        protected const float SelectedObjectWidth = HeaderHeight + 250.0f + HorizontalIndent / 2;

        protected static readonly Color BoxColor = new Color(0.0f, 0.0f, 0.0f, 0.2f);
        protected static readonly Color EmptyColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        protected const string FolderIconName = "d_Folder Icon";
        protected const string InspectorIconName = "d_UnityEditor.InspectorWindow";
        protected const string HierarchyIconName = "d_UnityEditor.SceneHierarchyWindow";

        protected const string SceneHierarchySearchReferenceFormat = "ref:{0}";

        protected static readonly Color ErrorColor = Color.red;

        private Vector2 ScrollViewPosition { get; set; }
        protected bool IsFoldersShown { get; set; } = false;

        private SearchHelperFilterManager FilterManager { get; set; }
        private SearchHelperSortManager SortManager { get; set; }

        protected abstract IEnumerable<ObjectContext> Data { get; }
        public bool IsGlobalScope { get; set; } = true;

        public virtual void Init()
        {
            FilterManager ??= new SearchHelperFilterManager(OnDataChanged);
            SortManager ??= new SearchHelperSortManager(OnDataChanged);
        }

        public abstract void Draw(Rect windowRect);

        public abstract void Run(Object selectedObject);
        public abstract void Run();

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

        #region Data

        


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
                    dependency.ShouldBeShown = ShouldBeShown(dependency, context);
                }
            }

            SortManager?.Sort(contexts);
        }
        protected virtual bool ShouldBeShown(ObjectContext objectContext, ObjectContext parentContext = null)
        {
            if (objectContext.IsFolder && !IsFoldersShown)
            {
                return false;
            }

            if (IsFilterRuleSupported && FilterManager != null)
            {
                if (!FilterManager.IsAllowed(objectContext, parentContext))
                {
                    return false;
                }
            }

            return true;
        }

        private void OnDataChanged()
        {
            UpdateData();
        }
        #endregion

        #region DrawFunctions

        protected void DrawHeaderControls()
        {
            EGuiKit.Horizontal(() =>
            {
                DrawSettingsRules();
                DrawScopeRules();
                DrawSortingRules();
                DrawRules();
                DrawFilterString();
            });
        }

        private void DrawSettingsRules()
        {
            if (!IsSettingsButtonEnabled)
            {
                return;
            }

            EGuiKit.Space(HorizontalIndent);
            var content = new GUIContent($"Settings");

            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();
                if (IsShowingFoldersSupported)
                {
                    menu.AddItem(new GUIContent("Show Folders"), IsFoldersShown, () =>
                    {
                        IsFoldersShown = !IsFoldersShown;
                        UpdateData();
                    });
                }

                menu.AddItem(new GUIContent("Sort Main Objects"), ShouldMainObjectsBeSorted,
                    () => { ShouldMainObjectsBeSorted = !ShouldMainObjectsBeSorted; });

                menu.AddItem(new GUIContent("Show File Size"), IsSizeShowingSupported,
                    () => { IsSizeShowingSupported = !IsSizeShowingSupported; });

                menu.AddItem(new GUIContent("Use Cache"), IsCacheUsed, () => { IsCacheUsed = !IsCacheUsed; });

                menu.AddItem(new GUIContent("Visibility/Show Dependencies"), false, () =>
                {
                    foreach (var context in Data)
                    {
                        context.IsExpanded = true;
                    }
                });

                menu.AddItem(new GUIContent("Visibility/Hide Dependencies"), false, () =>
                {
                    foreach (var context in Data)
                    {
                        context.IsExpanded = false;
                    }
                });

                menu.AddItem(new GUIContent("Visibility/Show Empty Dependencies"), IsEmptyDependencyShown,
                    () => { IsEmptyDependencyShown = !IsEmptyDependencyShown; });

                AddSettingsContextMenu(menu);

                menu.ShowAsContext();
            }

            EGuiKit.Space(HorizontalIndent);
        }

        protected virtual void AddSettingsContextMenu(GenericMenu menu)
        {
        }

        private void DrawScopeRules()
        {
            if (!IsScopeRulesSupported)
            {
                return;
            }

            EGuiKit.Space(HorizontalIndent);

            EGuiKit.Button(IsGlobalScope ? "Global" : "Local", () =>
            {
                IsGlobalScope = !IsGlobalScope;
                Run();
            });
        }

        private void DrawSortingRules()
        {
            if (!IsSortingSupported || SortManager == null)
            {
                return;
            }

            EGuiKit.Space(HorizontalIndent);

            var currentSortVariant = SortManager.CurrentSortVariant;
            var content = new GUIContent(currentSortVariant.ToString().ToSpacedWords());

            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();
                foreach (var sortVariant in SortManager.PossibleSortVariants)
                {
                    menu.AddItem(new GUIContent(sortVariant.ToString().ToSpacedWords()),
                        currentSortVariant == sortVariant, () => { SortManager.Select(sortVariant); });
                }

                menu.ShowAsContext();
            }
        }

        private void DrawRules()
        {
            if (!IsFilterRuleSupported || FilterManager == null)
            {
                return;
            }

            EGuiKit.Space(HorizontalIndent);

            var currentFilterName = FilterManager.CurrentFilterRule?.Name;
            var content = new GUIContent(currentFilterName ?? "No Filter Rule");

            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                FilterManager.UpdateFilterRulesIfEmpty();

                var menu = new GenericMenu();
                foreach (var rule in FilterManager.FilterRules)
                {
                    menu.AddItem(new GUIContent(rule.Name), currentFilterName == rule.Name,
                        () => { FilterManager.SelectFilterRule(rule); });
                }

                menu.AddItem(new GUIContent("Load more from disk"), false, FilterManager.UpdateFilterRules);
                menu.AddItem(new GUIContent("Remove Filter Rule"), false, FilterManager.UnselectFilterRule);
                menu.ShowAsContext();
            }
        }

        private void DrawFilterString()
        {
            if (!IsFilterStringSupported || FilterManager == null)
            {
                return;
            }

            EGuiKit.Space(HorizontalIndent);

            var target = FilterManager.CurrentFilterByStringTarget;
            var mode = FilterManager.CurrentFilterByStringMode;
            var filter = FilterManager.CurrentFilterString;

            var content = new GUIContent($"{target}");
            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();
                foreach (ObjectContextTarget possibleTarget in Enum.GetValues(typeof(ObjectContextTarget)))
                {
                    menu.AddItem(new GUIContent(possibleTarget.ToString()), target == possibleTarget,
                        () => { FilterManager.SelectFilterByString(possibleTarget, mode, filter); });
                }

                menu.ShowAsContext();
            }

            content = new GUIContent($"{ToString(mode)}:");
            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();
                foreach (FilterRuleMode possibleMode in Enum.GetValues(typeof(FilterRuleMode)))
                {
                    menu.AddItem(new GUIContent(ToString(possibleMode)), mode == possibleMode,
                        () => { FilterManager.SelectFilterByString(target, possibleMode, filter); });
                }

                menu.ShowAsContext();
            }

            FilterManager.SelectFilterByString(target, mode, EditorGUILayout.TextArea(filter, GUILayout.Width(250)));

            string ToString(FilterRuleMode mode)
            {
                switch (mode)
                {
                    case FilterRuleMode.Include:
                        return "contains";

                    default:
                    case FilterRuleMode.Exclude:
                        return "!contains";
                }
            }
        }

        protected void DrawVirtualScroll(Rect windowRect, List<ObjectContext> contexts, Model model = null)
        {
            if (contexts.IsNullOrEmpty())
            {
                return;
            }

            GUILayout.Space(HeaderPadding);

            ScrollViewPosition = EditorGUILayout.BeginScrollView(ScrollViewPosition,
                GUILayout.Height(windowRect.height - BottomIndent));

            var totalHeight = CalculateDisplayedHeight(contexts, model);
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
                        () => TryDrawObjectHeader(ref x, ref y, displayRect.width, ctx, model),
                        () => CalculateHeaderHeight(ctx, model)))
                {
                    break;
                }

                if ((!model?.DrawDependencies) ?? false)
                {
                    continue;
                }

                if (ctx.Dependencies.IsNullOrEmpty())
                {
                    if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect,
                            () => TryDrawEmptyContent(ref x, ref y, displayRect.width, ctx, model),
                            () => CalculateEmptyHeight(ctx, model)))
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

        private float CalculateDisplayedHeight(List<ObjectContext> contexts, Model model)
        {
            return contexts.Sum(ctx => CalculateHeaderHeight(ctx, model) + CalculateDependenciesHeight(ctx, model));
        }

        private bool TryDraw(ref float currentY, Vector2 scrollViewPosition, ref float drawnHeight, Rect windowRect,
            Func<float> tryDraw, Func<float> calculateHeight)
        {
            if (IsVisible(currentY, scrollViewPosition, windowRect, out _, out var afterVisibleRect))
            {
                drawnHeight += tryDraw?.Invoke() ?? 0.0f;
            }

            currentY += calculateHeight?.Invoke() ?? 0.0f;
            return !afterVisibleRect;
        }

        private float CalculateDependenciesHeight(ObjectContext context, Model model)
        {
            return context.Dependencies.IsNullOrEmpty()
                ? CalculateEmptyHeight(context, model)
                : context.Dependencies.Sum(dependency => CalculateDependencyHeight(dependency, context));
        }

        private float CalculateHeaderHeight(ObjectContext context, Model model)
        {
            if (!context.ShouldBeShown)
            {
                return 0.0f;
            }

            if (((!model?.DrawObjectWithEmptyDependencies) ?? false)
                && !context.Dependencies.Any(dependency => dependency.ShouldBeShown))
            {
                return 0.0f;
            }

            return HeaderHeightWithPadding;
        }

        private float TryDrawObjectHeader(ref float x, ref float y, float width, ObjectContext context, Model model)
        {
            if (CalculateHeaderHeight(context, model) == 0.0f)
            {
                return 0.0f;
            }

            var result = DrawObjectHeader(new Rect(x, y, width, HeaderHeightWithPadding), context, model);
            y += result;
            return result;
        }

        private float DrawObjectHeader(Rect rect, ObjectContext context, Model model)
        {
            var x = rect.x + FirstElementIndent;
            var y = rect.y;
            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, HeaderPadding), EmptyColor);
            y += HeaderPadding;

            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, HeaderHeight + HeaderPadding), BoxColor);
            y += HeaderPadding / 2;
            var elementWidth = 0.0f;

            if (model?.DrawMergeButtons ?? false)
            {
                elementWidth = 75.0f;
                var toggleRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
                var selected = EditorGUI.ToggleLeft(toggleRect, "Selected", context.IsSelected);
                if (selected != context.IsSelected)
                {
                    model?.OnSelectedButtonPressed?.Invoke(context);
                }

                x += elementWidth + HorizontalIndent / 2;
                elementWidth = 75.0f;
                var removeRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
                if (GUI.Button(removeRect, "Remove"))
                {
                    model?.OnRemoveButtonPressed?.Invoke(context);
                }

                x += elementWidth + HorizontalIndent / 2;
                var comporandRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
                if (GUI.Button(comporandRect, context.IsBaseObject ? "Base" : "Theirs"))
                {
                    model?.OnComparandButtonPressed?.Invoke(context);
                }

                x += elementWidth + HorizontalIndent / 2;
                var diffRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
                if (GUI.Button(diffRect, "Diff"))
                {
                    model?.OnDiffButtonPressed?.Invoke(context);
                }

                x += elementWidth + HorizontalIndent / 2;
            }

            elementWidth = HeaderHeight;
            if (GUI.Button(new Rect(x, y - 1, elementWidth, HeaderHeight),
                    EditorGUIUtility.IconContent(FolderIconName)))
            {
                OpenInDefaultFileBrowser(context);
            }

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = 250.0f;
            var objectFieldRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
            var objectColor = model?.GetObjectFieldColor != null ? model.GetObjectFieldColor(context) : GUI.color;

            EGuiKit.Color(objectColor,
                () => { EditorGUI.ObjectField(objectFieldRect, context.Object, typeof(Object), context.Object); });

            DrawContextMenu(context, objectFieldRect);

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = EditorStyles.foldoutHeader.CalcSize(new GUIContent(context.Path)).x;
            context.IsExpanded = EditorGUI.BeginFoldoutHeaderGroup(new Rect(x, y, elementWidth, HeaderHeight),
                context.IsExpanded, context.Path);
            EditorGUI.EndFoldoutHeaderGroup();

            x += elementWidth + HorizontalIndent;

            var leftWidth = rect.width - x;
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

            var neededWidthForDependency = neededWidthForGuid + 50.0f + 90.0f + HorizontalIndent;
            if (leftWidth > neededWidthForDependency)
            {
                elementWidth = 50.0f;
                x -= elementWidth + HorizontalIndent;
                EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight), context.Dependencies?.Count.ToString());

                elementWidth = 90.0f;
                x -= elementWidth;
                EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), "Dependencies:");
            }

            var neededWidthForSize = neededWidthForDependency + HorizontalIndent;
            if (IsSizeShowingSupported)
            {
                neededWidthForSize = neededWidthForDependency + 70 + 40.0f + HorizontalIndent;
                if (leftWidth > neededWidthForSize)
                {
                    elementWidth = 70.0f;
                    x -= elementWidth + HorizontalIndent;
                    EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight), context.Size ?? "");

                    elementWidth = 40.0f;
                    x -= elementWidth;
                    EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), "Size:");
                }
            }

            var neededWidthForState = neededWidthForSize + StateTextAreaWidth + HorizontalIndent;
            if (leftWidth > neededWidthForState)
            {
                if (model?.DrawState ?? true)
                {
                    var message = model?.GetState != null ? model.GetState(context) : null;
                    if (message.HasValue)
                    {
                        elementWidth = StateTextAreaWidth;
                        x -= elementWidth + HorizontalIndent;

                        EGuiKit.Color(message.Value.Item2,
                            () =>
                            {
                                EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight), message.Value.Item1);
                            });
                    }
                }
            }

            return HeaderHeightWithPadding;
        }

        private float CalculateEmptyHeight(ObjectContext mainContext, Model model)
        {
            if (mainContext.IsFolder)
            {
                if (!model?.DrawEmptyFolder ?? true)
                {
                    return 0.0f;
                }
            }

            if (!mainContext.IsExpanded)
            {
                return 0.0f;
            }

            if (IsFilterRuleSupported && !mainContext.ShouldBeShown)
            {
                return 0.0f;
            }

            if (!IsEmptyDependencyShown || (!model?.DrawEmptyDependency ?? false))
            {
                return 0.0f;
            }

            return ContentHeightWithPadding;
        }

        private float TryDrawEmptyContent(ref float x, ref float y, float width, ObjectContext mainContext, Model model)
        {
            if (CalculateEmptyHeight(mainContext, model) == 0.0f)
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
            EGuiKit.Color(ErrorColor,
                () =>
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

        private float TryDrawContent(ref float x, ref float y, float width, ObjectContext context,
            ObjectContext mainContext)
        {
            if (CalculateDependencyHeight(context, mainContext) == 0.0f)
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
            EditorGUI.ObjectField(objectFieldRect, context.Object, typeof(Object), context.Object);
            x += elementWidth + HorizontalIndent / 2;

            DrawContextMenu(context, objectFieldRect);

            elementWidth = ContentHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, HeaderHeight),
                    EditorGUIUtility.IconContent(HierarchyIconName)))
            {
                FindInHierarchyWindow(context);
            }

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = ContentHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, HeaderHeight),
                    EditorGUIUtility.IconContent(InspectorIconName)))
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
            if (GUI.Button(new Rect(x, rect.y, elementWidth, HeaderHeight),
                    EditorGUIUtility.IconContent(FolderIconName)))
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

        private void ShowContextMenu(ObjectContext context)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Open Folder"), false, () => { OpenInDefaultFileBrowser(context); });
            menu.AddItem(new GUIContent("Find in Project"), false, () => { FindInProject(context); });
            menu.AddItem(new GUIContent("Find in Scene"), false, () => { FindInHierarchyWindow(context); });
            menu.AddItem(new GUIContent("Properties"), false, () => { OpenProperty(context); });

            menu.AddItem(new GUIContent("Copy/Path"), false, () => { CopyToClipboard(context.Path); });
            menu.AddItem(new GUIContent("Copy/GUID"), false, () => { CopyToClipboard(context.Guid); });
            menu.AddItem(new GUIContent("Copy/Type"), false,
                () => { CopyToClipboard(context.Object.GetType().Name); });

            if (!context.Dependencies.IsNullOrEmpty())
            {
                menu.AddItem(new GUIContent("Select All in Project"), false, () => { SelectAll(context); });
                menu.AddItem(new GUIContent("Select Dependencies in Project"), false, () => { SelectDependencies(context); });
                menu.AddItem(new GUIContent("Copy/Dependency Paths"), false,
                    () =>
                    {
                        CopyToClipboard(string.Join(", ", context.Dependencies.Select(element => element.Path)));
                    });
            }

            AddContextMenu(menu, context);

            menu.ShowAsContext();
        }

        protected virtual void AddContextMenu(GenericMenu menu, ObjectContext context)
        {
        }

        #endregion

        protected IEnumerable<Object> FolderOrFile(Object obj)
        {
            var path = PathFromObject(obj);
            return string.IsNullOrEmpty(FolderPathFromObject(obj))
                ? obj.AsIEnumerable()
                : SearchHelperService.FindAssetObjects(path);
        }

        protected string PathFromObject(Object obj)
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

        protected void SelectDependencies(ObjectContext context)
        {
            if (context == null || context.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            Selection.objects = context.Dependencies.Select(el => el.Object).ToArray();
        }

        protected void SelectAll(ObjectContext context)
        {
            if (context == null || context.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            Selection.objects = new[] { context.Object }.Concat(context.Dependencies.Select(el => el.Object)).ToArray();
        }

        protected void FindInHierarchyWindow(ObjectContext context)
        {
            if (context?.Path == null)
            {
                return;
            }

            var sceneHierarchyType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            var window = EditorWindow.GetWindow(sceneHierarchyType);
            var method =
                sceneHierarchyType.GetMethod("SetSearchFilter", BindingFlags.Instance | BindingFlags.NonPublic);

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

        protected void OpenInDefaultFileBrowser(ObjectContext context)
        {
            if (string.IsNullOrEmpty(context?.Path))
            {
                return;
            }

            EditorUtility.RevealInFinder(context.Path);
        }

        protected void FindInProject(ObjectContext context)
        {
            if (context?.Object == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(context.Object);
        }

        protected void OpenProperty(ObjectContext context)
        {
            if (context?.Object == null)
            {
                return;
            }

            EditorUtility.OpenPropertyEditor(context.Object);
        }

        protected void CopyToClipboard(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
        }
    }
}