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
        public string Name { get; set; }

        public abstract void Draw(Rect windowRect);

        public abstract void Run(Object selectedObject);

        protected enum SortVariant
        {
            None = 0,
            ByName,
            ByPath
        }

        protected SortVariant CurrentSortVariant { get; set; }
        protected string FilterString { get; set; }
        protected bool IsFoldersShown { get; set; } = false;
        protected bool IsEditorBuiltInElementsShown { get; set; } = false;

        protected static readonly Color ImportantColor = Color.yellow;
        protected static readonly Color WarningColor = Color.yellow;
        protected static readonly Color ErrorColor = Color.red;

        protected const float DefaultSpace = 25;
        protected const string FolderIconName = "d_Folder Icon";
        protected const string EditorBuiltInPath = "Resources/unity_builtin_extra";

        protected GUILayoutOption MiddleWidth = GUILayout.Width(270);


        protected const float Intend = 20.0f;
        protected const float BoxIntend = 4.0f;

        protected const float RowHeight = 20.0f;
        protected const float RowPadding = 2f;

        protected const float ContentHeight = RowHeight;
        protected const float ContentPadding = RowPadding;
        protected const float ContentHeightWithPadding = ContentHeight + ContentPadding;

        protected const float HeaderHeight = ContentHeight;
        protected const float HeaderPadding = 6.0f;
        protected const float HeaderHeightWithPadding = ContentHeight + HeaderPadding * 2;

        protected static readonly Color BoxColor = new Color(0.0f, 0.0f, 0.0f, 0.2f);
        protected static readonly Color EmptyColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        public Vector2 ScrollViewPosition { get; set; }

        protected static bool IsVisible(float itemTopY, float itemHeight, float visibleTopY, float visibleBottomY)
        {
            var itemBottom = itemTopY + itemHeight;
            return !(itemBottom < visibleTopY || itemTopY > visibleBottomY);
        }

        protected void DrawVirtualScroll(Rect windowRect, List<ObjectContext> contexts)
        {
            if (contexts.IsNullOrEmpty())
            {
                return;
            }

            ScrollViewPosition = EditorGUILayout.BeginScrollView(ScrollViewPosition, GUILayout.Height(windowRect.height - 100));

            var totalHeight = CalculateDisplayedHeight(contexts);
            var fullRect = GUILayoutUtility.GetRect(0, totalHeight);

            var x = fullRect.x;
            var y = ScrollViewPosition.y;
            var currentY = 0.0f;
            var drawnHeight = 0.0f;

            foreach (var ctx in contexts)
            {
                if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, windowRect, 
                        () => TryDrawObjectHeader(ref x, ref y, windowRect.width, ctx),
                        () => CalculateHeaderHeight(ctx)))
                {
                    break;
                }

                if (ctx.Dependencies.IsNullOrEmpty())
                {
                    if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, windowRect, 
                            () => TryDrawEmptyContent(ref x, ref y, windowRect.width, ctx),
                            () => CalculateEmptyHeight(ctx)))
                    {
                        break;
                    }
                }
                else
                {
                    foreach (var dependency in Sort(ctx.Dependencies))
                    {
                        if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, windowRect, 
                                () => TryDrawContent(ref x, ref y, windowRect.width, dependency, ctx),
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
            var y = rect.y;
            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, HeaderPadding), EmptyColor);
            y += HeaderPadding;

            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, HeaderHeight + HeaderPadding), BoxColor);
            y += HeaderPadding / 2;

            var elementWidth = EditorStyles.foldoutHeader.CalcSize(new GUIContent(context.Path)).x;
            context.IsExpanded = EditorGUI.BeginFoldoutHeaderGroup(new Rect(rect.x + BoxIntend, y, elementWidth, HeaderHeight), context.IsExpanded, context.Path);

            elementWidth = 250.0f;
            var x = rect.width - elementWidth - BoxIntend ;
            EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight), context.Guid);

            elementWidth = 40.0f;
            x -= elementWidth;
            EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), "Guid:");

            elementWidth = 50.0f;
            x -= elementWidth + Intend;
            EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight), context.Dependencies?.Capacity.ToString());

            elementWidth = 100.0f;
            x -= elementWidth;
            EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), "Dependencies:");

            elementWidth = 250.0f;
            x -= elementWidth + Intend;
            EditorGUI.ObjectField(new Rect(x, y, elementWidth, HeaderHeight), context.Object, typeof(Object),
                context.Object);

            elementWidth = HeaderHeight;
            x -= elementWidth + Intend;
            if (GUI.Button(new Rect(x, y, elementWidth, HeaderHeight), EditorGUIUtility.IconContent(FolderIconName)))
            {
                if (!string.IsNullOrEmpty(context.Path))
                {
                    EditorUtility.RevealInFinder(context.Path);
                }
            }
            EditorGUI.EndFoldoutHeaderGroup();

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
            EditorGUI.LabelField(rect, "The object doesn't have any dependencies.");
            return ContentHeightWithPadding;
        }

        protected float CalculateDependencyHeight(ObjectContext dependency, ObjectContext mainContext)
        {
            if (!mainContext.IsExpanded)
            {
                return 0.0f;
            }

            if (!dependency.IsValid)
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
            var x = rect.x + BoxIntend;
            EditorGUI.ObjectField(new Rect(x, rect.y, elementWidth, ContentHeight), context.Object, typeof(Object),
                context.Object);
            x += elementWidth + Intend;

            elementWidth = 40.0f;
            EditorGUI.LabelField(new Rect(x, rect.y, elementWidth, ContentHeight), "Guid:");
            x += elementWidth;

            elementWidth = 250.0f;
            EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, ContentHeight), context.Guid);
            x += elementWidth + Intend;

            elementWidth = 40.0f;
            EditorGUI.LabelField(new Rect(x, rect.y, elementWidth, ContentHeight), "Path:");
            x += elementWidth;

            elementWidth = rect.width - x - BoxIntend;
            EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, ContentHeight), context.Path);

            return ContentHeightWithPadding;
        }

        protected void DrawHeaderControls()
        {
            EGuiKit.Horizontal(() =>
            {
                IsFoldersShown = EditorGUILayout.ToggleLeft("Show Folders", IsFoldersShown);
                EGuiKit.Space(DefaultSpace);
                IsEditorBuiltInElementsShown = EditorGUILayout.ToggleLeft("Show Editor Built-In", IsEditorBuiltInElementsShown);
                EGuiKit.Label("Sorting:");
                CurrentSortVariant = (SortVariant)GUILayout.Toolbar((int)CurrentSortVariant, System.Enum.GetNames(typeof(SortVariant)));
                EGuiKit.Space(DefaultSpace);

                EGuiKit.Label("Path Contains:");
                FilterString = EditorGUILayout.TextArea(FilterString, MiddleWidth);
                EGuiKit.Space(DefaultSpace);
            });
        }

        protected IEnumerable<ObjectContext> Sort(IEnumerable<ObjectContext> objectContexts)
        {
            switch (CurrentSortVariant)
            {
                case SortVariant.ByName:
                    return objectContexts.OrderBy(el => el.Object.name);
                    break;
                case SortVariant.ByPath:
                    return objectContexts.OrderBy(el => el.Path);
                case SortVariant.None:
                default:
                    return objectContexts;
            }
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
    }
}
