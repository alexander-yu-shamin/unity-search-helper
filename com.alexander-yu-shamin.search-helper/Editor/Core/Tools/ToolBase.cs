using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SearchHelper.Editor.Core;
using SearchHelper.Editor.Core.Filter;
using SearchHelper.Editor.Core.Sort;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Helpers.Diagnostics;
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
            public Func<Asset, string> GetEmptyAssetText { get; set; }
            public Func<Asset, string> GetSizeTooltipText { get; set; }

            // Merge
            public bool DrawMergeButtons { get; set; }
            public bool DrawState { get; set; } = true;

            public Func<Asset, (string, Color)?> GetAssetStateText { get; set; }
            public Action<Asset> OnSelectedButtonPressed { get; set; }
            public Action<Asset> OnRemoveButtonPressed { get; set; }
            public Action<Asset> OnComparandButtonPressed { get; set; }
            public Action<Asset> OnDiffButtonPressed { get; set; }
        }

        #region Capabilities

        protected virtual bool AreActionsSupported { get; set; } = true;

        // Settings
        protected virtual bool AreSettingsSupported { get; set; } = true;
        protected virtual bool ShowSize { get; set; } = false;
        protected virtual bool IsCacheUsed { get; set; } = true;
        protected virtual bool ShowDependenciesCount { get; set; } = true;
        protected virtual bool IsMetaDiffSupported { get; set; } = false;
        protected virtual bool MetaDiffEnabled { get; set; } = false;

        // Visibility
        protected virtual bool AreVisibilityRulesSupported { get; set; } = true;
        protected virtual bool AreShowingFoldersSupported { get; set; } = true;
        protected virtual bool ShowFolders { get; set; } = false;
        protected virtual bool ShowEmptyDependencyText { get; set; } = true;
        protected virtual bool CountHiddenDependencies { get; set; } = false;
        protected virtual bool ShowAssetWithNoDependencies { get; set; } = true;
        protected virtual bool ShowAssetWithDependencies { get; set; } = true;

        protected virtual bool AreScopeRulesSupported { get; set; } = false;
        public virtual bool IsGlobalScope { get; set; } = true;
        protected virtual bool AreSortingRulesSupported { get; set; } = true;
        protected virtual bool AreFilterByRuleSupported { get; set; } = true;
        protected virtual bool AreFilterByStringRulesSupported { get; set; } = true;
        protected virtual bool IsTransferSupported { get; set; } = true;
        #endregion

        #region DrawSettings

        protected const float RowHeight = 20.0f;
        protected const float RowPadding = 2f;

        protected const float ContentHeight = RowHeight;
        protected const float ContentPadding = RowPadding;
        protected const float ContentHeightWithPadding = ContentHeight + ContentPadding;

        protected const float HeaderHeight = ContentHeight;
        protected const float HeaderPadding = 6.0f;
        protected const float HeaderHeightWithPadding = ContentHeight + HeaderPadding * 2;
        protected const float HeaderIndent = 5.0f;

        protected const float HorizontalIndent = 15.0f;
        protected const float FirstElementIndent = 4.0f;
        protected const float ScrollBarWidth = 16.0f;
        protected const float NoScrollBarWidth = 4.0f;
        protected const float GuidTextAreaWidth = 275.0f;
        protected const float StateTextAreaWidth = 180.0f;
        protected const float ExtraHeightToPreventBlinking = ContentHeightWithPadding * 5;
        protected const float BottomIndent = ContentHeightWithPadding * 3;
        protected const float SelectedObjectWidth = HeaderHeight + 250.0f + HorizontalIndent / 2;

        protected static readonly Color RectBoxColor = new Color(0.0f, 0.0f, 0.0f, 0.2f);
        protected static readonly Color RectBoxEmptyColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        protected const string FolderIconName = "d_Folder Icon";
        protected const string InspectorIconName = "d_UnityEditor.InspectorWindow";
        protected const string HierarchyIconName = "d_UnityEditor.SceneHierarchyWindow";
        protected const string SceneHierarchySearchReferenceFormat = "ref:{0}";

        protected static readonly Color ErrorColor = Color.red;

        #endregion
        private Vector2 ScrollViewPosition { get; set; }
        private FilterByRuleManager FilterByRuleManager { get; set; }
        private FilterByStringManager FilterByStringManager { get; set; }
        private SortManager SortManager { get; set; }
        protected DiffManager DiffManager { get; set; }
        
        private List<SearchHelperWindow.ToolType> PossibleTransferTypes { get; set; } =
            new List<SearchHelperWindow.ToolType>()
            {
                SearchHelperWindow.ToolType.DependencyTool,
                SearchHelperWindow.ToolType.UsedByTool,
                SearchHelperWindow.ToolType.DuplicatesTool,
                SearchHelperWindow.ToolType.MergeTool
            };

        protected Model DefaultModel { get; set; }

        #region I
        protected abstract SearchHelperWindow.ToolType CurrentToolType { get; set; }
        protected abstract IEnumerable<Asset> Data { get; }
        public abstract void Run(Object selectedObject);
        public abstract void Run();
        public abstract void Draw(Rect windowRect);

        public virtual void GetDataFromAnotherTool(SearchHelperWindow.ToolType from,
            SearchHelperWindow.ToolType to, IEnumerable<Asset> assets)
        {
        }

        public virtual void GetDataFromAnotherTool(SearchHelperWindow.ToolType from,
            SearchHelperWindow.ToolType to, Asset asset)
        {
        }

        public virtual void AssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
        }

        public virtual void Init()
        {
            FilterByRuleManager = new FilterByRuleManager();
            FilterByStringManager = new FilterByStringManager();
            SortManager = new SortManager();
            DiffManager = new DiffManager();

            FilterByRuleManager.DataChanged += OnDataChanged;
            FilterByStringManager.DataChanged += OnDataChanged;
            SortManager.DataChanged += OnDataChanged;
            DiffManager.DataChanged += OnDataChanged;

            DefaultModel ??= new Model()
            {
                DrawMergeButtons = false,
                DrawState = false,
                GetAssetStateText = GetAssetStateText,
                GetEmptyAssetText = GetEmptyAssetText
            };
        }
        #endregion

        #region Data

        private void OnDataChanged()
        {
            UpdateAssets();
        }

        protected virtual bool IsMainAssetVisible(Asset asset)
        {
            return asset.State.HasNoneFlags(AssetState.None
                                            | AssetState.FilterByRule
                                            | AssetState.FilterByString
                                            | AssetState.HideFolders
                                            | AssetState.HideEmptyDependencies
                                            | AssetState.HideDependencies);
        }

        protected virtual bool IsDependencyAssetVisible(Asset asset)
        {
            return asset.State.HasNoneFlags(AssetState.None
                                            | AssetState.FilterByRule
                                            | AssetState.FilterByString
                                            | AssetState.HideFolders);
        }

        protected void UpdateAssets(IEnumerable<Asset> assets = null, bool forceUpdate = false)
        {
            assets ??= Data;

            if (assets.IsNullOrEmpty())
            {
                return;
            }

            using (Profiler.Measure($"UpdateData::State"))
            {
                var useFilterByRule = forceUpdate || AreFilterByRuleSupported && FilterByRuleManager is { RequiresUpdate: true };
                var useFilterByString = forceUpdate || AreFilterByStringRulesSupported && FilterByStringManager is { RequiresUpdate: true };
                var useMetaDiff = forceUpdate || IsMetaDiffSupported && DiffManager is { RequiresUpdate: true };
                foreach (var asset in assets)
                {
                    asset.State &= ~AssetState.HideFolders;
                    asset.State &= ~AssetState.HideEmptyDependencies;
                    asset.State &= ~AssetState.HideDependencies;

                    //if (AreShowingFoldersSupported && asset.IsFolder)
                    //{
                    //    asset.State = ShowFolders 
                    //        ? asset.State & ~AssetState.HideFolders
                    //        : asset.State | AssetState.HideFolders;
                    //}
                    if (asset.IsFolder)
                    {
                        if (!AreShowingFoldersSupported)
                        {
                            asset.State |= AssetState.HideFolders;
                        }
                        else
                        {
                            asset.State = ShowFolders
                                ? asset.State & ~AssetState.HideFolders
                                : asset.State | AssetState.HideFolders;
                        }
                    }

                    if (asset.Dependencies.IsNullOrEmpty())
                    {
                        asset.State = ShowAssetWithNoDependencies
                            ? asset.State & ~AssetState.HideEmptyDependencies
                            : asset.State | AssetState.HideEmptyDependencies;
                    }
                    else
                    {
                        asset.State = ShowAssetWithDependencies
                            ? asset.State & ~AssetState.HideDependencies
                            : asset.State | AssetState.HideDependencies;
                    }

                    if (useFilterByRule)
                    {
                        asset.State = FilterByRuleManager.IsAllowed(asset)
                            ? asset.State & ~AssetState.FilterByRule
                            : asset.State | AssetState.FilterByRule;
                    }

                    if (useMetaDiff)
                    {
                        if (MetaDiffEnabled)
                        {
                            UpdateDiff(asset);
                        }
                        else
                        {
                            ClearMetaState(asset);
                        }
                    }

                    if ((asset.State & AssetState.FilterByRule) != 0)
                    {
                        continue;
                    }

                    if (!useFilterByString && !useFilterByRule)
                    {
                        continue;
                    }

                    var isAllowed = FilterByStringManager.IsAllowed(asset);
                    if (!asset.Dependencies.IsNullOrEmpty())
                    {
                        foreach (var dependency in asset.Dependencies)
                        {
                            var isDependencyAllowed = (dependency.State & AssetState.FilterByRule) == 0;
                            if (useFilterByRule)
                            {
                                isDependencyAllowed = FilterByRuleManager.IsAllowed(dependency);
                                dependency.State = isDependencyAllowed
                                    ? dependency.State & ~AssetState.FilterByRule
                                    : dependency.State | AssetState.FilterByRule;
                            }

                            if (!isDependencyAllowed)
                            {
                                continue;
                            }

                            isDependencyAllowed = FilterByStringManager.IsAllowed(dependency);

                            dependency.State = isDependencyAllowed
                                ? dependency.State & ~AssetState.FilterByString
                                : dependency.State | AssetState.FilterByString;

                            isAllowed |= isDependencyAllowed;
                        }
                    }

                    asset.State = isAllowed
                        ? asset.State & ~AssetState.FilterByString
                        : asset.State | AssetState.FilterByString;
                }

                FilterByRuleManager.CompleteUpdate();
                FilterByStringManager.CompleteUpdate();
            }

            using (Profiler.Measure($"UpdateData:: Sorting"))
            {
                if (AreSortingRulesSupported && SortManager is { RequiresUpdate: true })
                {
                    SortManager.Sort(assets);
                    SortManager.CompleteUpdate();
                }
            }
        }

        protected virtual void UpdateDiff(Asset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (asset.Dependencies.IsNullOrEmpty())
            {
                asset.MetaDiffState = AssetDiffState.None;
                return;
            }

            asset.MetaDiffState = AssetDiffState.BaseObject;
            foreach (var dependency in asset.Dependencies)
            {
                var result = DiffManager.CompareMetaFiles(asset.MetaPath, dependency.MetaPath);
                dependency.MetaDiffState = result.HasValue
                    ? result.Value 
                        ? AssetDiffState.SameAsBaseObject 
                        : AssetDiffState.NotTheSameAsBaseObject
                    : AssetDiffState.None;
            }
        }

        protected virtual void ClearMetaState(Asset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (asset.Dependencies.IsNullOrEmpty())
            {
                asset.MetaDiffState = AssetDiffState.None;
                return;
            }

            asset.MetaDiffState = AssetDiffState.None;
            foreach (var dependency in asset.Dependencies)
            {
                dependency.MetaDiffState = AssetDiffState.None;
            }
        }
        #endregion

        #region DrawFunctions

        protected void DrawHeaderControls()
        {
            EGuiKit.Horizontal(() =>
            {
                DrawActions();
                DrawSettingsRules();
                DrawVisibilityRules();
                DrawScopeRules();
                DrawSortingRules();
                DrawFilterRules();
                DrawFilterString();
            });
        }

        private void DrawActions()
        {
            if (!AreActionsSupported)
            {
                return;
            }

            EGuiKit.Space(HeaderIndent);
            var content = new GUIContent($"Actions");

            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Copy/Asset Paths"), false, () =>
                {
                    CopyToClipboard(string.Join("\n", Data?.Where(IsMainAssetVisible)?.Select(asset => asset.Path) ?? Array.Empty<string>()));
                });

                menu.AddItem(new GUIContent("Copy/Asset Dependency Map"), false, () =>
                {
                    if (Data.IsNullOrEmpty())
                    {
                        return;
                    }

                    var sb = new StringBuilder();
                    foreach (var asset in Data)
                    {
                        if (!IsMainAssetVisible(asset))
                        {
                            continue;
                        }

                        sb.AppendLine($"- {asset.Path}");
                        if (asset.Dependencies.IsNullOrEmpty())
                        {
                            continue;
                        }

                        foreach (var dependency in asset.Dependencies.Where(IsDependencyAssetVisible).Select(d => d.Path).Distinct())
                        {
                            sb.AppendLine($"  - {dependency}");
                        }

                        sb.AppendLine();
                    }

                    CopyToClipboard(sb.ToString());
                });

                AddActionContextMenu(menu);

                menu.ShowAsContext();
            }
        }

        protected virtual void AddActionContextMenu(GenericMenu menu)
        {
        }

        private void DrawSettingsRules()
        {
            if (!AreSettingsSupported)
            {
                return;
            }

            EGuiKit.Space(HeaderIndent);
            var content = new GUIContent($"Settings");

            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Show File Size"), ShowSize,
                    () => { ShowSize = !ShowSize; });

                menu.AddItem(new GUIContent("Use Cache"), IsCacheUsed, () => { IsCacheUsed = !IsCacheUsed; });

                if (IsMetaDiffSupported && DiffManager != null)
                {
                    var ignoredLines = DiffManager.IgnoredLines;
                    var possibleLines = DiffManager.PossibleIgnoredLines;

                    menu.AddItem(new GUIContent("Meta Diff/Enabled"), MetaDiffEnabled, () =>
                    {
                        MetaDiffEnabled = !MetaDiffEnabled;
                        DiffManager.UpdateState();
                    });

                    menu.AddSeparator("Meta Diff/");

                    foreach (var ignoredLine in possibleLines)
                    {
                        AddItem(ignoredLine);
                    }

                    menu.AddItem(new GUIContent($"Meta Diff/Add your line"), false, () =>
                    {
                        InputDialog.Show("Add Ignore Line", "", result =>
                        {
                            if (!string.IsNullOrEmpty(result))
                            {
                                DiffManager.AddToIgnoreLines(result);
                                MetaDiffSettingsUpdated();
                            }
                        });
                    });

                    void AddItem(string line)
                    {
                        var containLine = ignoredLines.Contains(line);
                        menu.AddItem(new GUIContent($"Meta Diff/Ignore line with: {line}"), ignoredLines.Contains(line), () =>
                        {
                            if (containLine)
                            {
                                DiffManager.RemoveLine(line);
                            }
                            else
                            {
                                DiffManager.AddToIgnoreLines(line);
                            }

                            MetaDiffSettingsUpdated();
                        });
                    }
                }

                AddSettingsContextMenu(menu);

                menu.ShowAsContext();
            }
        }

        protected virtual void MetaDiffSettingsUpdated()
        {
        }

        protected virtual void AddSettingsContextMenu(GenericMenu menu)
        {
        }

        private void DrawVisibilityRules()
        {
            if (!AreVisibilityRulesSupported)
            {
                return;
            }

            EGuiKit.Space(HeaderIndent);
            var content = new GUIContent($"Visibility");

            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Expand All"), false, () =>
                {
                    foreach (var asset in Data)
                    {
                        asset.IsFoldout = true;
                    }
                });

                menu.AddItem(new GUIContent("Collapse All"), false, () =>
                {
                    foreach (var asset in Data)
                    {
                        asset.IsFoldout = false;
                    }
                });

                menu.AddSeparator(string.Empty);
                if (AreShowingFoldersSupported)
                {
                    menu.AddItem(new GUIContent("Show Folders"), ShowFolders, () =>
                    {
                        ShowFolders = !ShowFolders;
                        UpdateAssets();
                    });
                }

                menu.AddItem(new GUIContent("Show Asset with No Dependencies"), ShowAssetWithNoDependencies, () =>
                {
                    ShowAssetWithNoDependencies = !ShowAssetWithNoDependencies;
                    UpdateAssets();
                });

                menu.AddItem(new GUIContent("Show Asset with Dependencies"), ShowAssetWithDependencies, () =>
                {
                    ShowAssetWithDependencies = !ShowAssetWithDependencies;
                    UpdateAssets();
                });

                menu.AddItem(new GUIContent("Count Hidden Dependencies"), CountHiddenDependencies, () =>
                {
                    CountHiddenDependencies = !CountHiddenDependencies;
                    UpdateAssets();
                });

                menu.AddItem(new GUIContent("Show Empty Dependency Text"), ShowEmptyDependencyText, () =>
                {
                    ShowEmptyDependencyText = !ShowEmptyDependencyText;
                    UpdateAssets();
                });

                menu.ShowAsContext();
            }
        }

        private void DrawScopeRules()
        {
            if (!AreScopeRulesSupported)
            {
                return;
            }

            EGuiKit.Space(HeaderIndent);

            EGuiKit.Button(IsGlobalScope ? "Global" : "Local", () =>
            {
                IsGlobalScope = !IsGlobalScope;
                Run();
            });
        }

        private void DrawSortingRules()
        {
            if (!AreSortingRulesSupported || SortManager == null)
            {
                return;
            }

            EGuiKit.Space(HeaderIndent);

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

                menu.AddSeparator(string.Empty);

                var currentSortOrder = SortManager.CurrentSortOrder;
                foreach (var sortOrder in SortManager.PossibleSortOrders)
                {
                    menu.AddItem(new GUIContent(sortOrder.ToString()), sortOrder == currentSortOrder,
                        () => { SortManager.Select(sortOrder); });
                }

                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Sort Main Assets"), SortManager.SortMainAssets,
                    () => { SortManager.SortMainAssets = !SortManager.SortMainAssets; });

                menu.ShowAsContext();
            }
        }

        private void DrawFilterRules()
        {
            if (!AreFilterByRuleSupported || FilterByRuleManager == null)
            {
                return;
            }

            EGuiKit.Space(HeaderIndent);

            var currentFilterName = FilterByRuleManager.CurrentFilterRule?.Name;
            var content = new GUIContent(currentFilterName ?? "No Filter Rule");

            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                FilterByRuleManager.UpdateFilterRulesIfEmpty();

                var menu = new GenericMenu();
                foreach (var rule in FilterByRuleManager.FilterRules)
                {
                    menu.AddItem(new GUIContent(rule.Name), currentFilterName == rule.Name,
                        () => { FilterByRuleManager.SelectFilterRule(rule); });
                }

                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Load more from disk"), false, FilterByRuleManager.UpdateFilterRules);
                menu.AddItem(new GUIContent("Remove Filter Rule"), false, FilterByRuleManager.UnselectFilterRule);
                menu.ShowAsContext();
            }
        }

        private void DrawFilterString()
        {
            if (!AreFilterByStringRulesSupported || FilterByStringManager == null)
            {
                return;
            }

            EGuiKit.Space(HeaderIndent);

            var target = FilterByStringManager.CurrentFilterByStringTarget;
            var mode = FilterByStringManager.CurrentFilterByStringMode;
            var filter = FilterByStringManager.CurrentFilterString;

            var content = new GUIContent($"{target}");
            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();
                foreach (var possibleTarget in FilterByStringManager.PossibleAssetTargets)
                {
                    menu.AddItem(new GUIContent(possibleTarget.ToString()), target == possibleTarget,
                        () => { FilterByStringManager.SelectFilterByString(possibleTarget, mode, filter); });
                }

                menu.ShowAsContext();
            }

            content = new GUIContent($"{ToString(mode)}:");
            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                var menu = new GenericMenu();
                foreach (var possibleMode in FilterByStringManager.PossibleFilterRuleModes)
                {
                    menu.AddItem(new GUIContent(ToString(possibleMode)), mode == possibleMode,
                        () => { FilterByStringManager.SelectFilterByString(target, possibleMode, filter); });
                }

                menu.ShowAsContext();
            }

            FilterByStringManager.SelectFilterByString(target, mode,
                EditorGUILayout.TextArea(filter, GUILayout.Width(250)));

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

        protected void DrawVirtualScroll(Rect windowRect, List<Asset> assets, Model model = null)
        {
            if (assets.IsNullOrEmpty())
            {
                return;
            }

            if (model == null)
            {
                model = DefaultModel;
            }

            GUILayout.Space(HeaderPadding);

            ScrollViewPosition = EditorGUILayout.BeginScrollView(ScrollViewPosition,
                GUILayout.Height(windowRect.height - BottomIndent));

            var totalHeight = CalculateDisplayedHeight(assets, model);
            var fullRect = GUILayoutUtility.GetRect(0, totalHeight);

            var x = fullRect.x;
            var y = ScrollViewPosition.y;
            var currentY = 0.0f;
            var drawnHeight = 0.0f;

            var displayRect = new Rect(0, 0,
                totalHeight > windowRect.height
                    ? windowRect.width - ScrollBarWidth
                    : windowRect.width - NoScrollBarWidth, windowRect.height + ExtraHeightToPreventBlinking);

            foreach (var ctx in assets)
            {
                if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect,
                        () => TryDrawObjectHeader(ref x, ref y, displayRect.width, ctx, model),
                        () => CalculateHeaderHeight(ctx, model)))
                {
                    break;
                }

                if (ctx.Dependencies.IsNullOrEmpty())
                {
                    if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect,
                            () => TryDrawEmptyDependency(ref x, ref y, displayRect.width, ctx, model),
                            () => CalculateEmptyDependencyHeight(ctx, model)))
                    {
                        break;
                    }
                }
                else
                {
                    foreach (var dependency in ctx.Dependencies)
                    {
                        if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect,
                                () => TryDrawDependency(ref x, ref y, displayRect.width, dependency, ctx, model),
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

        private float CalculateDisplayedHeight(List<Asset> assets, Model model)
        {
            return assets.Sum(ctx => CalculateHeaderHeight(ctx, model) + CalculateDependenciesHeight(ctx, model));
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

        private float CalculateDependenciesHeight(Asset asset, Model model)
        {
            return asset.Dependencies.IsNullOrEmpty()
                ? CalculateEmptyDependencyHeight(asset, model)
                : asset.Dependencies.Sum(dependency => CalculateDependencyHeight(dependency, asset));
        }

        private float CalculateHeaderHeight(Asset asset, Model model)
        {
            if (!IsMainAssetVisible(asset))
            {
                return 0.0f;
            }

            return HeaderHeightWithPadding;
        }

        private float TryDrawObjectHeader(ref float x, ref float y, float width, Asset asset, Model model)
        {
            if (CalculateHeaderHeight(asset, model) == 0.0f)
            {
                return 0.0f;
            }

            var result = DrawObjectHeader(new Rect(x, y, width, HeaderHeightWithPadding), asset, model);
            y += result;
            return result;
        }

        private float DrawObjectHeader(Rect rect, Asset asset, Model model)
        {
            var x = rect.x + FirstElementIndent;
            var y = rect.y;
            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, HeaderPadding), RectBoxEmptyColor);
            y += HeaderPadding;

            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, HeaderHeight + HeaderPadding), RectBoxColor);
            y += HeaderPadding / 2;
            var elementWidth = 0.0f;

            if (model?.DrawMergeButtons ?? false)
            {
                elementWidth = 75.0f;
                var toggleRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
                var selected = EditorGUI.ToggleLeft(toggleRect, "Selected", asset.IsSelected);
                if (selected != asset.IsSelected)
                {
                    model?.OnSelectedButtonPressed?.Invoke(asset);
                }

                x += elementWidth + HorizontalIndent / 2;
                elementWidth = 75.0f;
                var removeRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
                if (GUI.Button(removeRect, "Remove"))
                {
                    model?.OnRemoveButtonPressed?.Invoke(asset);
                }

                x += elementWidth + HorizontalIndent / 2;
                var comporandRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
                if (GUI.Button(comporandRect, asset.IsBaseObject ? "Base" : "Theirs"))
                {
                    model?.OnComparandButtonPressed?.Invoke(asset);
                }

                x += elementWidth + HorizontalIndent / 2;
                var diffRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
                if (GUI.Button(diffRect, "Diff"))
                {
                    model?.OnDiffButtonPressed?.Invoke(asset);
                }

                x += elementWidth + HorizontalIndent / 2;
            }

            elementWidth = HeaderHeight;
            if (GUI.Button(new Rect(x, y - 1, elementWidth, HeaderHeight),
                    EditorGUIUtility.IconContent(FolderIconName)))
            {
                OpenInDefaultFileBrowser(asset);
            }

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = 250.0f;
            var objectFieldRect = new Rect(x, y - 1, elementWidth, HeaderHeight);
            var assetState = model?.GetAssetStateText?.Invoke(asset);
            var objectColor = assetState?.Item2 ?? GUI.color;

            EGuiKit.Color(objectColor,
                () => { EditorGUI.ObjectField(objectFieldRect, asset.Object, typeof(Object), asset.Object); });

            DrawContextMenu(asset, objectFieldRect);

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = EditorStyles.foldoutHeader.CalcSize(new GUIContent(asset.Path)).x;
            asset.IsFoldout = EditorGUI.BeginFoldoutHeaderGroup(new Rect(x, y, elementWidth, HeaderHeight), asset.IsFoldout, asset.Path);

            EditorGUI.EndFoldoutHeaderGroup();

            x += elementWidth + HorizontalIndent;

            var leftWidth = rect.width - x;
            var neededWidthForGuid = GuidTextAreaWidth + 40.0f;

            if (leftWidth > neededWidthForGuid)
            {
                elementWidth = GuidTextAreaWidth;
                x = rect.width - elementWidth;
                EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight), asset.Guid);

                elementWidth = 40.0f;
                x -= elementWidth;
                EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), "GUID:");
            }

            var neededWidthForDependency = neededWidthForGuid + HorizontalIndent;
            if (ShowDependenciesCount)
            {
                neededWidthForDependency = neededWidthForGuid + 50.0f + 90.0f + HorizontalIndent;
                if (leftWidth > neededWidthForDependency)
                {
                    elementWidth = 50.0f;
                    x -= elementWidth + HorizontalIndent;
                    EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight),
                        !CountHiddenDependencies
                            ? asset.Dependencies.Count(IsDependencyAssetVisible).ToString()
                            : asset.Dependencies?.Count.ToString());

                    elementWidth = 90.0f;
                    x -= elementWidth;
                    EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), "Dependencies:");
                }
            }

            var neededWidthForSize = neededWidthForDependency + HorizontalIndent;
            if (ShowSize)
            {
                neededWidthForSize = neededWidthForDependency + 70 + 40.0f + HorizontalIndent;
                if (leftWidth > neededWidthForSize)
                {
                    elementWidth = 70.0f;
                    x -= elementWidth + HorizontalIndent;
                    EditorGUI.TextArea(new Rect(x, y, elementWidth, HeaderHeight), asset.ReadableSize ?? "");

                    elementWidth = 40.0f;
                    x -= elementWidth;
                    EditorGUI.LabelField(new Rect(x, y, elementWidth, HeaderHeight), new GUIContent("Size:", model?.GetSizeTooltipText?.Invoke(asset)));
                }
            }

            var neededWidthForState = neededWidthForSize + StateTextAreaWidth + HorizontalIndent;
            if (leftWidth > neededWidthForState)
            {
                if (model?.DrawState ?? true)
                {
                    var message = model?.GetAssetStateText?.Invoke(asset);
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

        private float CalculateEmptyDependencyHeight(Asset mainAsset, Model model)
        {
            if (!IsMainAssetVisible(mainAsset))
            {
                return 0.0f;
            }

            if (!mainAsset.IsFoldout)
            {
                return 0.0f;
            }

            if (!ShowEmptyDependencyText)
            {
                return 0.0f;
            }

            return ContentHeightWithPadding;
        }

        private float TryDrawEmptyDependency(ref float x, ref float y, float width, Asset mainAsset, Model model)
        {
            if (CalculateEmptyDependencyHeight(mainAsset, model) == 0.0f)
            {
                return 0.0f;
            }

            var result = DrawEmptyDependency(new Rect(x, y, width, ContentHeightWithPadding), model.GetEmptyAssetText(mainAsset) ?? "None");
            y += result;
            return result;
        }

        private float DrawEmptyDependency(Rect rect, string text)
        {
            EditorGUI.DrawRect(rect, RectBoxColor);
            EGuiKit.Color(ErrorColor,
                () =>
                {
                    EditorGUI.LabelField(new Rect(rect.x + FirstElementIndent, rect.y, rect.width, rect.height), text);
                });
            return ContentHeightWithPadding;
        }

        private float CalculateDependencyHeight(Asset dependency, Asset mainAsset)
        {
            if (!IsMainAssetVisible(mainAsset))
            {
                return 0.0f;
            }

            if (!mainAsset.IsFoldout)
            {
                return 0.0f;
            }

            if (!IsDependencyAssetVisible(dependency))
            {
                return 0.0f;
            }

            return ContentHeightWithPadding;
        }

        private float TryDrawDependency(ref float x, ref float y, float width, Asset asset,
            Asset mainAsset, Model model)
        {
            if (CalculateDependencyHeight(asset, mainAsset) == 0.0f)
            {
                return 0.0f;
            }

            var result = DrawDependency(new Rect(x, y, width, ContentHeightWithPadding), asset, mainAsset, model);
            y += result;
            return result;
        }

        private float DrawDependency(Rect rect, Asset asset, Asset mainAsset, Model model)
        {
            EditorGUI.DrawRect(rect, RectBoxColor);

            var elementWidth = 500.0f;
            var x = rect.x + FirstElementIndent;
            var objectFieldRect = new Rect(x, rect.y, elementWidth, ContentHeight);

            var assetState = model?.GetAssetStateText?.Invoke(asset);
            var objectColor = assetState?.Item2 ?? GUI.color;

            EGuiKit.Color(objectColor, () =>
            {
                EditorGUI.ObjectField(objectFieldRect, asset.Object, typeof(Object), asset.Object);
            });

            x += elementWidth + HorizontalIndent / 2;

            DrawContextMenu(asset, objectFieldRect, mainAsset);

            elementWidth = ContentHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, HeaderHeight),
                    EditorGUIUtility.IconContent(HierarchyIconName)))
            {
                FindInHierarchyWindow(asset);
            }

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = ContentHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, HeaderHeight),
                    EditorGUIUtility.IconContent(InspectorIconName)))
            {
                OpenProperty(asset);
            }

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = 40.0f;
            EditorGUI.LabelField(new Rect(x, rect.y, elementWidth, ContentHeight), "GUID:");
            x += elementWidth;

            elementWidth = GuidTextAreaWidth;
            EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, ContentHeight), asset.Guid);
            x += elementWidth + HorizontalIndent / 2;

            elementWidth = ContentHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, HeaderHeight),
                    EditorGUIUtility.IconContent(FolderIconName)))
            {
                if (!string.IsNullOrEmpty(asset.Path))
                {
                    EditorUtility.RevealInFinder(asset.Path);
                }
            }

            x += elementWidth + HorizontalIndent / 2;
            elementWidth = 40.0f;
            EditorGUI.LabelField(new Rect(x, rect.y, elementWidth, ContentHeight), "Path:");
            x += elementWidth;

            var drawState = (model?.DrawState ?? false) && (asset.DiffState != AssetDiffState.None || asset.MetaDiffState != AssetDiffState.None);
            if (drawState)
            {
                elementWidth = rect.width - x - StateTextAreaWidth;
            }
            else
            {
                elementWidth = rect.width - x;
            }

            EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, ContentHeight), asset.Path);
            x += elementWidth;

            if (drawState)
            {
                var message = model?.GetAssetStateText?.Invoke(asset);
                if (message.HasValue)
                {
                    elementWidth = StateTextAreaWidth;
                    EGuiKit.Color(message.Value.Item2,
                        () =>
                        {
                            EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, HeaderHeight), message.Value.Item1);
                        });
                }
            }

            return ContentHeightWithPadding;
        }

        private void DrawContextMenu(Asset asset, Rect objectFieldRect, Asset mainAsset = null)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1 && objectFieldRect.Contains(e.mousePosition))
            {
                ShowContextMenu(asset, mainAsset);
            }
        }

        private void ShowContextMenu(Asset asset, Asset mainAsset = null)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Open Folder"), false, () => { OpenInDefaultFileBrowser(asset); });
            menu.AddItem(new GUIContent("Find in Project"), false, () => { FindInProject(asset); });
            menu.AddItem(new GUIContent("Find in Scene"), false, () => { FindInHierarchyWindow(asset); });
            menu.AddItem(new GUIContent("Properties"), false, () => { OpenProperty(asset); });

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy/Path"), false, () => { CopyToClipboard(asset.Path); });
            menu.AddItem(new GUIContent("Copy/GUID"), false, () => { CopyToClipboard(asset.Guid); });
            menu.AddItem(new GUIContent("Copy/Type"), false, () => { CopyToClipboard(asset.Object.GetType().Name); });

            if (!asset.Dependencies.IsNullOrEmpty())
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Select All in Project"), false, () => { SelectAll(asset); });
                menu.AddItem(new GUIContent("Select Dependencies in Project"), false,
                    () => { SelectDependencies(asset); });
                menu.AddItem(new GUIContent("Copy/Dependency Paths"), false,
                    () =>
                    {
                        CopyToClipboard(string.Join(", ", asset.Dependencies.Select(element => element.Path)));
                    });
            }

            if (IsTransferSupported && asset.Object != null)
            {
                menu.AddSeparator(string.Empty);
                foreach (var transferType in PossibleTransferTypes)
                {
                    menu.AddItem(new GUIContent($"Transfer To/{transferType.ToString().ToSpacedWords()}"), false, () =>
                    {
                        TransferTo(CurrentToolType, transferType, asset);
                    });
                }
            }

            if (IsMetaDiffSupported)
            {
                menu.AddSeparator(string.Empty);

                if (mainAsset != null)
                {
                    menu.AddItem(new GUIContent("Diff/Asset"), false, () => { InvokeDiffTool(mainAsset.Path, mainAsset.Path, asset.Path, asset.Path); });
                    menu.AddItem(new GUIContent("Diff/Meta"), false, () => { InvokeDiffTool(mainAsset.MetaPath, mainAsset.MetaPath, asset.MetaPath, asset.MetaPath); });
                }
                else
                {
                    menu.AddItem(new GUIContent("Diff/Meta Diff"), false, () => { UpdateDiff(asset); });
                }
            }

            AddContextMenu(menu, asset);

            menu.ShowAsContext();
        }

        protected virtual void AddContextMenu(GenericMenu menu, Asset asset)
        {
        }

        protected virtual Object DrawObject(Object obj, Action<Object> onNewObject = null)
        {
            return EGuiKit.Object(obj, typeof(Object), true, onNewObject, GUILayout.Width(SelectedObjectWidth), GUILayout.Height(HeaderHeight));
        }

        /// <summary>
        /// Returns text description and color coding for asset meta diff state only:
        /// - Gray: Meta state is None (error/missing)
        /// - Magenta: Meta differs from base
        /// - Yellow: Meta is base object
        /// - Green: Meta matches base
        /// </summary>
        protected virtual (string, Color)? GetAssetStateText(Asset asset)
        {
            return asset.MetaDiffState switch
            {
                AssetDiffState.NotTheSameAsBaseObject => ("Meta mismatch base", Color.magenta),
                AssetDiffState.BaseObject             => ("Base object", Color.yellow),
                AssetDiffState.SameAsBaseObject       => ("Meta matches Base", Color.green),
                _                                     => null
            };
        }

        protected virtual string GetEmptyAssetText(Asset mainAsset)
        {
            if (AreScopeRulesSupported)
            {
                return IsGlobalScope ? "The asset is not referenced anywhere in the project." : "The asset is not referenced locally.";
            }
            else
            {
                return "The asset is not referenced anywhere in the project.";
            }
        } 
        #endregion

        #region Helpers

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

        protected void SelectDependencies(Asset asset)
        {
            if (asset == null || asset.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            Selection.objects = asset.Dependencies.Select(el => el.Object).ToArray();
        }

        protected void SelectAll(Asset asset)
        {
            if (asset == null || asset.Dependencies.IsNullOrEmpty())
            {
                return;
            }

            Selection.objects = new[] { asset.Object }.Concat(asset.Dependencies.Select(el => el.Object)).ToArray();
        }

        protected void FindInHierarchyWindow(Asset asset)
        {
            if (asset?.Path == null)
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
                string.Format(SceneHierarchySearchReferenceFormat, asset.Path),
                0, // SearchableEditorWindow.SearchMode.All
                false, // setAll
                false // delayed
            });
        }

        protected void OpenInDefaultFileBrowser(Asset asset)
        {
            if (string.IsNullOrEmpty(asset?.Path))
            {
                return;
            }

            EditorUtility.RevealInFinder(asset.Path);
        }

        protected void FindInProject(Asset asset)
        {
            if (asset?.Object == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(asset.Object);
        }

        protected void OpenProperty(Asset asset)
        {
            if (asset?.Object == null)
            {
                return;
            }

            EditorUtility.OpenPropertyEditor(asset.Object);
        }

        protected void CopyToClipboard(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
        }
        
        protected void TransferTo(SearchHelperWindow.ToolType from, SearchHelperWindow.ToolType to, Asset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (!IsMainAssetVisible(asset))
            {
                return;
            }

            var transferContext = new Asset(asset)
            {
                Dependencies = asset.Dependencies.Where(IsDependencyAssetVisible).ToList()
            };

            SearchHelperWindow.TransferToTool(from, to, transferContext);
        }

        protected void InvokeDiffTool(string leftTitle, string leftFile, string rightTitle, string rightFile)
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
        #endregion
    }
}