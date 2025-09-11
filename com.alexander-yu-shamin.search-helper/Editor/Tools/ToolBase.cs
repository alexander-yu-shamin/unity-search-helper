using System;
using System.Collections.Generic;
using System.Linq;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor
{
    public abstract class ToolBase
    {
        public abstract string Name { get; set; }

        public virtual bool IsSortingSupported { get; set; } = true;

        protected enum SortVariant
        {
            None = 0,
            ByName,
            ByPath
        }

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
        protected const string EditorBuiltInPath = "Resources/unity_builtin_extra";

        protected static readonly Color ImportantColor = Color.yellow;
        protected static readonly Color WarningColor = Color.yellow;
        protected static readonly Color ErrorColor = Color.red;

        protected Vector2 ScrollViewPosition { get; set; }
        protected SortVariant CurrentSortVariant { get; set; }
        protected string FilterString { get; set; }
        protected bool IsFoldersShown { get; set; } = false;
        protected bool IsEditorBuiltInElementsShown { get; set; } = false;

        public abstract void Draw(Rect windowRect);

        public abstract void Run(Object selectedObject);

        protected virtual bool Sort(SortVariant sortVariant)
        {
            return false;
        }

        protected void DrawVirtualScroll(Rect windowRect, List<ObjectContext> contexts)
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

        protected bool IsVisible(float accumulatedHeight, Vector2 scrollViewPosition, Rect visibleRect,
            out bool beforeVisibleRect, out bool afterVisibleRect)
        {
            beforeVisibleRect = accumulatedHeight < scrollViewPosition.y;
            afterVisibleRect = accumulatedHeight >= scrollViewPosition.y + visibleRect.height;
            return !beforeVisibleRect && !afterVisibleRect;
        }

        protected float CalculateDisplayedHeight(List<ObjectContext> contexts)
        {
            return contexts.Sum(ctx => CalculateHeaderHeight(ctx) + CalculateDependenciesHeight(ctx));
        }

        protected bool TryDraw(ref float currentY, Vector2 scrollViewPosition, ref float drawnHeight, Rect windowRect, Func<float> tryDraw, Func<float> calculateHeight)
        {
            if (IsVisible(currentY, scrollViewPosition, windowRect, out _, out var afterVisibleRect))
            {
                drawnHeight += tryDraw?.Invoke() ?? 0.0f;
            }

            currentY += calculateHeight?.Invoke() ?? 0.0f;
            return !afterVisibleRect;
        }

        protected float CalculateDependenciesHeight(ObjectContext context)
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

            return HeaderHeightWithPadding;
        }

        protected float TryDrawObjectHeader(ref float x, ref float y, float width, ObjectContext context)
        {
            if (context.IsFolder && !IsFoldersShown)
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
            if (GUI.Button(new Rect(x, y - 1, elementWidth, HeaderHeight), EditorGUIUtility.IconContent(FolderIconName)))
            {
                if (!string.IsNullOrEmpty(context.Path))
                {
                    EditorUtility.RevealInFinder(context.Path);
                }
            }

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = 250.0f;
            EditorGUI.ObjectField(new Rect(x, y - 1, elementWidth, HeaderHeight), context.Object, typeof(Object),
                context.Object);

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = EditorStyles.foldoutHeader.CalcSize(new GUIContent(context.Path)).x;
            context.IsExpanded = EditorGUI.BeginFoldoutHeaderGroup(new Rect(x, y, elementWidth, HeaderHeight), context.IsExpanded, context.Path);
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
                EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), "Guid:");
            }

            if (leftWidth > neededWidthForDependency)
            {
                elementWidth = 50.0f;
                x -= elementWidth + HorizontalIndent;
                EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight), context.Dependencies?.Capacity.ToString());

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

            return ContentHeight;
        }

        protected float TryDrawEmptyContent(ref float x, ref float y, float width, ObjectContext mainContext)
        {
            if (mainContext.IsFolder)
            {
                return 0.0f;
            }

            if (!mainContext.IsExpanded)
            {
                return 0.0f;
            }

            var result = DrawEmptyContent(new Rect(x, y, width, ContentHeightWithPadding), mainContext);
            y += result;
            return result;
        }

        protected float DrawEmptyContent(Rect rect, ObjectContext mainContext)
        {
            EditorGUI.DrawRect(rect, BoxColor);
            EditorGUI.LabelField(new Rect(rect.x + FirstElementIndent, rect.y, rect.width, rect.height), "The object doesn't have any dependencies.");
            return ContentHeightWithPadding;
        }

        protected float CalculateDependencyHeight(ObjectContext dependency, ObjectContext mainContext)
        {
            if (!mainContext.IsExpanded)
            {
                return 0.0f;
            }

            if (!ShouldBeShown(dependency))
            {
                return 0.0f;
            }

            return ContentHeightWithPadding;
        }

        protected float TryDrawContent(ref float x, ref float y, float width, ObjectContext context, ObjectContext mainContext)
        {
            if (!mainContext.IsExpanded)
            {
                return 0.0f;
            }

            if (!ShouldBeShown(context))
            {
                return 0.0f;
            }

            var result = DrawContent(new Rect(x, y, width, ContentHeightWithPadding), context);
            y += result;
            return result;
        }

        protected float DrawContent(Rect rect, ObjectContext context)
        {
            EditorGUI.DrawRect(rect, BoxColor);

            var elementWidth = 500.0f;
            var x = rect.x + FirstElementIndent;
            EditorGUI.ObjectField(new Rect(x, rect.y, elementWidth, ContentHeight), context.Object, typeof(Object),
                context.Object);
            x += elementWidth + HorizontalIndent;

            elementWidth = 40.0f;
            EditorGUI.LabelField(new Rect(x, rect.y, elementWidth, ContentHeight), "Guid:");
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

        protected void DrawHeaderControls()
        {
            EGuiKit.Horizontal(() =>
            {
                IsFoldersShown = EditorGUILayout.ToggleLeft("Show Folders", IsFoldersShown, GUILayout.Width(100));
                EGuiKit.Space(HorizontalIndent);
                IsEditorBuiltInElementsShown = EditorGUILayout.ToggleLeft("Show Editor Built-In", IsEditorBuiltInElementsShown, GUILayout.Width(150));

                if (IsSortingSupported)
                {
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

                EGuiKit.Label("Path Contains:");
                FilterString = EditorGUILayout.TextArea(FilterString, GUILayout.Width(250));
                EGuiKit.Space(HorizontalIndent);
            });
        }

        protected bool ShouldBeShown(ObjectContext objectContext)
        {
            if (string.IsNullOrEmpty(objectContext.Path))
            {
                return true;
            }

            if (!IsEditorBuiltInElementsShown)
            {
                if (objectContext.Path == EditorBuiltInPath)
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(FilterString))
            {
                if (!objectContext.Path.Contains(FilterString))
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
                case SortVariant.None:
                default:
                    return objectContexts;
            }
        }
    }
}
