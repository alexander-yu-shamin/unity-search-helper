using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SearchHelper.Editor.Core.Filter;
using SearchHelper.Editor.Core.Sort;
using SearchHelper.Editor.UI;
using Toolkit.Editor.Attributes;
using Toolkit.Editor.Helpers.Diagnostics;
using Toolkit.Editor.Helpers.IMGUI;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor.Core.Tools
{
    public abstract class ToolBase : IEditorPrefs
    {
        protected class DrawModel
        {
            public Func<Asset, string> GetEmptyAssetText { get; set; }
            public Func<string> GetEmptyCollectionText { get; set; }
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
        [EditorPrefs(true)]
        protected virtual bool IsLogViewSupported { get; set; } = true;
        protected virtual bool AreActionsSupported { get; set; } = true;

        // Settings
        protected virtual bool AreSettingsSupported { get; set; } = true;
        [EditorPrefs(false)]
        protected virtual bool ShowSize { get; set; }
        [EditorPrefs(true)]
        protected virtual bool IsCacheUsed { get; set; } = true;
        protected virtual bool ShowDependenciesCount { get; set; } = true;
        protected virtual bool IsMetaDiffSupported { get; set; }
        protected virtual bool MetaDiffEnabled { get; set; }
        [EditorPrefs(true)]
        protected virtual bool ShowPath { get; set; } = true;

        // Visibility
        protected virtual bool AreVisibilityRulesSupported { get; set; } = true;
        protected virtual bool AreShowingFoldersSupported { get; set; } = true;

        [EditorPrefs(false)]
        protected virtual bool ShowFolders { get; set; }
        [EditorPrefs(true)]
        protected virtual bool ShowEmptyDependencyText { get; set; } = true;
        [EditorPrefs(false)]
        protected virtual bool CountHiddenDependencies { get; set; }
        [EditorPrefs(true)] 
        protected virtual bool ShowAssetWithNoDependencies { get; set; } = true;
        [EditorPrefs(true)] 
        protected virtual bool ShowAssetWithDependencies { get; set; } = true;

        protected virtual bool AreScopeRulesSupported { get; set; }
        public virtual bool IsGlobalScope { get; set; } = true;
        protected virtual bool AreSortingRulesSupported { get; set; } = true;
        protected virtual bool AreFilterByRuleSupported { get; set; } = true;
        protected virtual bool AreFilterByStringRulesSupported { get; set; } = true;
        protected virtual bool IsTransferSupported { get; set; } = true;
        protected virtual uint HeaderLineCount { get; set; } = 2;
        #endregion

        private Vector2 ScrollViewPosition { get; set; }
        private FilterByRuleManager FilterByRuleManager { get; set; }
        private FilterByStringManager FilterByStringManager { get; set; }
        private SortManager SortManager { get; set; }
        protected DiffManager DiffManager { get; set; }
        protected DrawModel DefaultDrawModel { get; set; }
        protected Rect CurrentToolRect { get; set; }
        protected bool IsFullScreenMode => SearchHelperWindow.IsFullScreenMode;
        private Logger.Logger Logger { get; set; }

        private List<SearchHelperWindow.ToolType> PossibleTransferTypes { get; set; } =
            new List<SearchHelperWindow.ToolType>()
            {
                SearchHelperWindow.ToolType.Dependency,
                SearchHelperWindow.ToolType.UsedBy,
                SearchHelperWindow.ToolType.Duplicates,
                SearchHelperWindow.ToolType.Merge
            };

        #region I
        public abstract string EditorPrefsPrefix { get; }
        protected abstract SearchHelperWindow.ToolType CurrentToolType { get; set; }
        protected abstract IEnumerable<Asset> Data { get; }
        public abstract void Run(Object selectedObject);
        public abstract void Run();
        public abstract void InnerDraw(Rect windowRect);

        public void Draw(Rect windowRect)
        {
            CurrentToolRect = windowRect;
            InnerDraw(windowRect);
        }

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

        protected void Log(LogType logType, string message)
        {
            Logger?.AddLog(logType, message);
        }

        public virtual void Init()
        {
            Logger = new Logger.Logger(5);
            this.LoadSettings();

            FilterByRuleManager = new FilterByRuleManager();
            FilterByStringManager = new FilterByStringManager();
            SortManager = new SortManager();
            DiffManager = new DiffManager();

            FilterByRuleManager.DataChanged += OnDataChanged;
            FilterByStringManager.DataChanged += OnDataChanged;
            SortManager.DataChanged += OnDataChanged;
            DiffManager.DataChanged += OnDataChanged;

            DefaultDrawModel ??= new ToolBase.DrawModel()
            {
                DrawMergeButtons = false,
                DrawState = false,
                GetEmptyCollectionText = GetEmptyCollectionText,
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

        protected void DrawHeaderLines(Action firstLineLeft = null, Action<GenericMenu> firstLineRight = null, Action secondLineLeft = null, Action secondLineRight = null)
        {
            var menu = new GenericMenu();
            EGuiKit.Vertical(() =>
            {
                EGuiKit.Horizontal(() =>
                {
                    firstLineLeft?.Invoke();
                    EGuiKit.FlexibleSpace();
                    firstLineRight?.Invoke(menu);
                    DrawActions(menu);
                    DrawScopeRules(menu);
                    DrawVisibilityRules(menu);
                    DrawSortingRules(menu);
                    DrawFilterRules(menu);
                    DrawSettingsRules(menu);
                });

                EGuiKit.Space(UISettings.HeaderPadding);

                EGuiKit.Horizontal(() =>
                {
                    secondLineLeft?.Invoke();
                    EGuiKit.FlexibleSpace();
                    secondLineRight?.Invoke();
                    DrawFilterString();
                });
            });
        }

        protected void DrawLogView()
        {
            if (IsLogViewSupported)
            {
                var y = CurrentToolRect.yMax - UISettings.LogViewHeight - UISettings.LogViewPadding;

                var textAreaRect = new Rect(UISettings.LogViewIndent, y, CurrentToolRect.width - UISettings.LogViewHeight - UISettings.LogViewIndent * 2, UISettings.LogViewHeight);
                EGuiKit.Enable(false, () =>
                {
                    var messageInfo = Logger.Peek();
                    EGuiKit.Color(ToColor(messageInfo.LogType), () =>
                    {
                        EditorGUI.TextArea(textAreaRect, messageInfo.Message);
                    });
                });
                var dropdownRect = new Rect(CurrentToolRect.xMax - UISettings.LogViewHeight, y, UISettings.LogViewHeight, UISettings.LogViewHeight);
                if (EditorGUI.DropdownButton(dropdownRect, GUIContent.none, FocusType.Passive))
                {
                    var menu = new GenericMenu();
                    foreach (var element in Logger.History())
                    {
                        menu.AddDisabledItem(new GUIContent(element));
                    }
                    menu.ShowAsContext();
                }
            }

            Color ToColor(LogType logType)
            {
                switch (logType)
                {
                    case LogType.Error:
                        return new Color(1f, 0.4f, 0.4f); // #FF6666
                    case LogType.Assert:
                        return new Color(0.4f, 0.8f, 1f); // #66CCFF
                    case LogType.Warning:
                        return new Color(1f, 0.9f, 0.3f); // #FFE64D
                    case LogType.Log:
                        return new Color(0.9f, 0.9f, 0.9f); // #E6E6E6
                    case LogType.Exception:
                        return new Color(1f, 0.3f, 0f); // #FF4D00
                    default:
                        return new Color(0.5f, 0.5f, 0.5f); // #808080
                }
            }
        }

        protected void DrawMain(Action firstLineLeft = null, Action<GenericMenu> firstLineRight = null, Action secondLineLeft = null, Action secondLineRight = null, Action drawContent = null)
        {
            DrawHeaderLines(firstLineLeft, firstLineRight, secondLineLeft, secondLineRight);
            drawContent?.Invoke();
            DrawLogView();
        }

        private void DrawActions(GenericMenu externalMenu)
        {
            if (!AreActionsSupported)
            {
                return;
            }

            if (IsFullScreenMode)
            {
                EGuiKit.Space(UISettings.HeaderSpace);
                var content = new GUIContent($"Actions");
                EGuiKit.DropdownButton(content, () =>
                {
                    var internalMenu = new GenericMenu();
                    AddToMenu(internalMenu, string.Empty);
                    internalMenu.ShowAsContext();
                });
            }
            else
            {
                var prefix = "Actions/";
                AddToMenu(externalMenu, prefix);
            }

            return;

            void AddToMenu(GenericMenu menu, string prefix)
            {
                menu.AddItem(new GUIContent(prefix + "Copy/Asset Paths"), false, () =>
                {
                    CopyToClipboard(string.Join("\n", Data?.Where(IsMainAssetVisible)?.Select(asset => asset.Path) ?? Array.Empty<string>()));
                });

                menu.AddItem(new GUIContent(prefix + "Copy/Asset Dependency Map"), false, () =>
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

                AddActionContextMenu(menu, prefix);
            }
        }

        protected virtual void AddActionContextMenu(GenericMenu menu, string prefix)
        {
        }

        private void DrawSettingsRules(GenericMenu menu)
        {
            if (!AreSettingsSupported)
            {
                return;
            }

            EGuiKit.Space(UISettings.HeaderSpace);

            var content = new GUIContent($"Settings");
            if (EditorGUILayout.DropdownButton(content, FocusType.Passive))
            {
                if (IsFullScreenMode)
                {
                    AddToMenu(menu, string.Empty);
                }
                else
                {
                    var prefix = "Settings/";
                    AddToMenu(menu, prefix);
                }

                menu.ShowAsContext();
            }

            return;

            void AddToMenu(GenericMenu menu, string prefix)
            {
                if (IsMetaDiffSupported && DiffManager != null)
                {
                    var ignoredLines = DiffManager.IgnoredLines;
                    var possibleLines = DiffManager.PossibleIgnoredLines;

                    menu.AddItem(new GUIContent(prefix + "Meta Diff/Enabled"), MetaDiffEnabled, () =>
                    {
                        MetaDiffEnabled = !MetaDiffEnabled;
                        DiffManager.UpdateState();
                    });

                    menu.AddSeparator(prefix + "Meta Diff/");

                    foreach (var ignoredLine in possibleLines)
                    {
                        AddItem(ignoredLine);
                    }

                    menu.AddItem(new GUIContent(prefix + $"Meta Diff/Add your line"), false, () =>
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
                        menu.AddItem(new GUIContent(prefix + $"Meta Diff/Ignore line with: {line}"),
                            ignoredLines.Contains(line), () =>
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

                menu.AddItem(new GUIContent(prefix + "Show File Size"), ShowSize, () =>
                {
                    ShowSize = !ShowSize;
                    this.SaveProperty(nameof(ShowSize));
                });
                menu.AddItem(new GUIContent(prefix + "Use Cache"), IsCacheUsed, () =>
                {
                    IsCacheUsed = !IsCacheUsed;
                    this.SaveProperty(nameof(IsCacheUsed));
                });
                menu.AddItem(new GUIContent(prefix + "Show Log"), IsLogViewSupported, () =>
                {
                    IsLogViewSupported = !IsLogViewSupported;
                    this.SaveProperty(nameof(IsLogViewSupported));
                });
                menu.AddItem(new GUIContent(prefix + "Show Path"), ShowPath, () =>
                {
                    ShowPath = !ShowPath;
                    this.SaveProperty(nameof(ShowPath));
                });

                menu.AddSeparator(prefix);
                
                menu.AddItem(new GUIContent(prefix + "Screen Mode: " + (SearchHelperWindow.ForceFullScreenMode.HasValue ? SearchHelperWindow.ForceFullScreenMode.Value ? "Full" : "Window" : "Dynamic")), SearchHelperWindow.ForceFullScreenMode.HasValue, () =>
                {
                    SearchHelperWindow.ForceFullScreenMode = SearchHelperWindow.ForceFullScreenMode.HasValue ? SearchHelperWindow.ForceFullScreenMode.Value ? false : null : true;
                });

                menu.AddItem(new GUIContent(prefix + "Reset Settings"), false, this.ResetAndLoadDefaults);

                AddSettingsContextMenu(menu);
            }
        }

        protected virtual void MetaDiffSettingsUpdated()
        {
        }

        protected virtual void AddSettingsContextMenu(GenericMenu menu)
        {
        }

        private void DrawVisibilityRules(GenericMenu externalMenu)
        {
            if (!AreVisibilityRulesSupported)
            {
                return;
            }

            if (IsFullScreenMode)
            {
                EGuiKit.Space(UISettings.HeaderSpace);
                var content = new GUIContent($"Visibility");
                EGuiKit.DropdownButton(content, () =>
                {
                    var internalMenu = new GenericMenu();
                    AddToMenu(internalMenu, string.Empty);
                    internalMenu.ShowAsContext();
                });
            }
            else
            {
                var prefix = "Visability/";
                AddToMenu(externalMenu, prefix);
            }

            return;

            void AddToMenu(GenericMenu menu, string prefix)
            {
                menu.AddItem(new GUIContent(prefix + "Expand All"), false, () =>
                {
                    foreach (var asset in Data)
                    {
                        asset.IsFoldout = true;
                    }
                });

                menu.AddItem(new GUIContent(prefix + "Collapse All"), false, () =>
                {
                    foreach (var asset in Data)
                    {
                        asset.IsFoldout = false;
                    }
                });

                menu.AddSeparator(prefix);
                if (AreShowingFoldersSupported)
                {
                    menu.AddItem(new GUIContent(prefix + "Show Folders"), ShowFolders, () =>
                    {
                        ShowFolders = !ShowFolders;
                        this.SaveProperty(nameof(ShowFolders));
                        UpdateAssets();
                    });
                }

                menu.AddItem(new GUIContent(prefix + "Show Asset with No Dependencies"), ShowAssetWithNoDependencies, () =>
                {
                    ShowAssetWithNoDependencies = !ShowAssetWithNoDependencies;
                    this.SaveProperty(nameof(ShowAssetWithNoDependencies));
                    UpdateAssets();
                });

                menu.AddItem(new GUIContent(prefix + "Show Asset with Dependencies"), ShowAssetWithDependencies, () =>
                {
                    ShowAssetWithDependencies = !ShowAssetWithDependencies;
                    this.SaveProperty(nameof(ShowAssetWithDependencies));
                    UpdateAssets();
                });

                menu.AddItem(new GUIContent(prefix + "Count Hidden Dependencies"), CountHiddenDependencies, () =>
                {
                    CountHiddenDependencies = !CountHiddenDependencies;
                    this.SaveProperty(nameof(CountHiddenDependencies));
                    UpdateAssets();
                });

                menu.AddItem(new GUIContent(prefix + "Show Empty Dependency Text"), ShowEmptyDependencyText, () =>
                {
                    ShowEmptyDependencyText = !ShowEmptyDependencyText;
                    this.SaveProperty(nameof(ShowEmptyDependencyText));
                    UpdateAssets();
                });
            }
        }

        private void DrawScopeRules(GenericMenu externalMenu)
        {
            if (!AreScopeRulesSupported)
            {
                return;
            }

            if (IsFullScreenMode)
            {
                EGuiKit.Space(UISettings.HeaderSpace);
                var content = new GUIContent("Global");
                EGuiKit.Button(IsGlobalScope ? "Global" : "Local", () =>
                {
                    IsGlobalScope = !IsGlobalScope;
                    Run();
                });
            }
            else
            {
                var prefix = "Scope Rules/";
                externalMenu.AddItem(new GUIContent(prefix + "Global"), IsGlobalScope, () =>
                {
                    IsGlobalScope = !IsGlobalScope;
                    Run();
                });
                externalMenu.AddItem(new GUIContent(prefix + "Local"), !IsGlobalScope, () =>
                {
                    IsGlobalScope = !IsGlobalScope;
                    Run();
                });
            }
        }

        private void DrawSortingRules(GenericMenu externalMenu)
        {
            if (!AreSortingRulesSupported || SortManager == null)
            {
                return;
            }

            var currentSortVariant = SortManager.CurrentSortVariant;

            if (IsFullScreenMode)
            {
                EGuiKit.Space(UISettings.HeaderSpace);
                var content = new GUIContent(currentSortVariant.ToString().ToSpacedWords());
                EGuiKit.DropdownButton(content, () =>
                {
                    var internalMenu = new GenericMenu();
                    AddToMenu(internalMenu, string.Empty);
                    internalMenu.ShowAsContext();
                });
            }
            else
            {
                AddToMenu(externalMenu, "Sorting/");
            }

            return;
                
            void AddToMenu(GenericMenu menu, string prefix)
            {
                foreach (var sortVariant in SortManager.PossibleSortVariants)
                {
                    menu.AddItem(new GUIContent(prefix + sortVariant.ToString().ToSpacedWords()),
                        currentSortVariant == sortVariant, () =>
                        {
                            SortManager.Select(sortVariant);
                        });
                }

                menu.AddSeparator(prefix + string.Empty);

                var currentSortOrder = SortManager.CurrentSortOrder;
                foreach (var sortOrder in SortManager.PossibleSortOrders)
                {
                    menu.AddItem(new GUIContent(prefix + sortOrder.ToString()), sortOrder == currentSortOrder, () =>
                    {
                        SortManager.Select(sortOrder);
                    });
                }

                menu.AddSeparator(prefix + string.Empty);
                menu.AddItem(new GUIContent(prefix + "Sort Main Assets"), SortManager.SortMainAssets, () =>
                {
                    SortManager.SortMainAssets = !SortManager.SortMainAssets;
                });
            }
        }

        private void DrawFilterRules(GenericMenu externalMenu)
        {
            if (!AreFilterByRuleSupported || FilterByRuleManager == null)
            {
                return;
            }

            var currentFilterName = FilterByRuleManager.CurrentFilterRule?.Name;

            if (IsFullScreenMode)
            {
                EGuiKit.Space(UISettings.HeaderSpace);
                var content = new GUIContent(currentFilterName ?? "No Filter Rule");
                EGuiKit.DropdownButton(content, () =>
                {
                    FilterByRuleManager.UpdateFilterRulesIfEmpty();
                    var internalMenu = new GenericMenu();
                    var currentName = FilterByRuleManager.CurrentFilterRule?.Name;
                    AddToMenu(internalMenu, string.Empty, currentName);
                    internalMenu.ShowAsContext();
                });
            }
            else
            {
                FilterByRuleManager.UpdateFilterRulesIfEmpty();

                var prefix = "Filter Rules/";
                externalMenu.AddItem(new GUIContent(prefix + $"Filter Rule: {(currentFilterName ?? "None".Replace("/", ": "))}"), !string.IsNullOrEmpty(currentFilterName), null);
                externalMenu.AddSeparator(prefix);
                AddToMenu(externalMenu, prefix, currentFilterName);
            }

            return;

            void AddToMenu(GenericMenu menu, string prefix, string currentFilterName)
            {
                foreach (var rule in FilterByRuleManager.FilterRules)
                {
                    menu.AddItem(new GUIContent(prefix + rule.Name), currentFilterName == rule.Name, () =>
                    {
                        FilterByRuleManager.SelectFilterRule(rule);
                    });
                }

                menu.AddSeparator(prefix);
                menu.AddItem(new GUIContent(prefix + "Load more from disk"), false, FilterByRuleManager.UpdateFilterRules);
                menu.AddItem(new GUIContent(prefix + "Remove Filter Rule"), false, FilterByRuleManager.UnselectFilterRule);
            }
        }

        private void DrawFilterString()
        {
            if (!AreFilterByStringRulesSupported || FilterByStringManager == null)
            {
                return;
            }

            EGuiKit.Space(UISettings.HeaderSpace);

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
                EditorGUILayout.TextArea(filter, GUILayout.ExpandWidth(true),
                    GUILayout.MinWidth(UISettings.FilterStringWidth), GUILayout.Height(UISettings.HeaderHeight)));

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

        protected virtual Rect CalculateVirtualScrollRect(Rect rect)
        {
            return new Rect(
                rect.x + UISettings.CommonLeftIndent, 
                rect.y + (HeaderLineCount * UISettings.AssetHeaderHeight) + UISettings.AssetHeaderPadding, 
                rect.width,
                rect.height - (HeaderLineCount * (UISettings.AssetHeaderHeight + UISettings.AssetHeaderPadding)) - UISettings.AssetHeaderPadding - (IsLogViewSupported ? UISettings.LogViewHeight + UISettings.LogViewPadding + UISettings.LogViewPadding : 0.0f));
        }

        protected void DrawVirtualScroll(List<Asset> assets, DrawModel drawModel = null)
        {
            if (assets == null)
            {
                return;
            }

            drawModel ??= DefaultDrawModel;

            if (assets.IsNullOrEmpty())
            {
                EGuiKit.Horizontal(
                    () =>
                    {
                        EGuiKit.Color(UISettings.ErrorColor, () =>
                        {
                            EGuiKit.Label(drawModel.GetEmptyCollectionText?.Invoke());
                        });
                    }, GUI.skin.box);
                return;
            }

            var rect = CalculateVirtualScrollRect(CurrentToolRect);

            EGuiKit.Space(UISettings.AssetHeaderPadding);
            
            ScrollViewPosition = EditorGUILayout.BeginScrollView(ScrollViewPosition, GUILayout.Height(rect.height));

            var totalHeight = CalculateDisplayedHeight(assets, drawModel);
            var fullRect = GUILayoutUtility.GetRect(0, totalHeight);

            var x = rect.x;
            var y = ScrollViewPosition.y;
            var currentY = 0.0f;
            var drawnHeight = 0.0f;

            var displayRect = new Rect(x, y,
                totalHeight > rect.height
                    ? rect.width - UISettings.CommonScrollBarWidth
                    : rect.width - UISettings.CommonNoScrollBarWidth,
                rect.height + UISettings.ExtraHeightToPreventBlinking * 2);

            foreach (var ctx in assets)
            {
                var wasDrawHeight = drawnHeight;
                if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect,
                        () => TryDrawAssetHeader(ref x, ref y, displayRect.width, ctx, drawModel),
                        () => CalculateAssetHeaderHeight(ctx, drawModel)))
                {
                    break;
                }

                var shouldAddSeparator = wasDrawHeight != drawnHeight;

                if (ctx.Dependencies.IsNullOrEmpty())
                {
                    if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect,
                            () => TryDrawEmptyDependency(ref x, ref y, displayRect.width, ctx, drawModel),
                            () => CalculateEmptyDependencyHeight(ctx, drawModel)))
                    {
                        break;
                    }
                }
                else
                {
                    foreach (var dependency in ctx.Dependencies)
                    {
                        if (!TryDraw(ref currentY, ScrollViewPosition, ref drawnHeight, displayRect,
                                () => TryDrawDependency(ref x, ref y, displayRect.width, dependency, ctx, drawModel),
                                () => CalculateDependencyHeight(dependency, ctx)))
                        {
                            break;
                        }
                    }
                }

                if (shouldAddSeparator)
                {
                    EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, UISettings.AssetHeaderSeparator), UISettings.SeparatorColor);
                    y += UISettings.AssetHeaderSeparator;
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

        private float CalculateDisplayedHeight(List<Asset> assets, ToolBase.DrawModel drawModel)
        {
            if (assets.IsNullOrEmpty())
            {
                return 0.0f;
            }

            var result = assets.Sum(ctx => CalculateAssetHeaderHeight(ctx, drawModel) + CalculateDependenciesHeight(ctx, drawModel));

            if (assets.Count == 1)
            {
                result -= UISettings.AssetHeaderSeparator;
            }

            return result;
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

        private float CalculateDependenciesHeight(Asset asset, ToolBase.DrawModel drawModel)
        {
            return asset.Dependencies.IsNullOrEmpty()
                ? CalculateEmptyDependencyHeight(asset, drawModel)
                : asset.Dependencies.Sum(dependency => CalculateDependencyHeight(dependency, asset));
        }

        private float CalculateAssetHeaderHeight(Asset asset, ToolBase.DrawModel drawModel)
        {
            if (!IsMainAssetVisible(asset))
            {
                return 0.0f;
            }

            return UISettings.AssetHeaderHeightWithPadding + UISettings.AssetHeaderSeparator;
        }

        private float TryDrawAssetHeader(ref float x, ref float y, float width, Asset asset, DrawModel drawModel)
        {
            if (CalculateAssetHeaderHeight(asset, drawModel) == 0.0f)
            {
                return 0.0f;
            }

            var result = DrawAssetHeader(new Rect(x, y, width, UISettings.AssetHeaderHeightWithPadding), asset, drawModel);
            y += result;
            return result;
        }

        private float DrawAssetHeader(Rect rect, Asset asset, ToolBase.DrawModel drawModel)
        {
            var x = rect.x + UISettings.AssetHeaderFirstElementIndent;
            var y = rect.y;

            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, UISettings.AssetHeaderHeightWithPadding), UISettings.AssetHeaderRectBoxColor);
            y += UISettings.AssetHeaderPadding / 2;
            var elementWidth = 0.0f;

            if (drawModel?.DrawMergeButtons ?? false)
            {
                elementWidth = UISettings.AssetHeaderHeight;
                var toggleRect = new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight);
                var selected = EditorGUI.Toggle(toggleRect, asset.IsSelected);
                if (selected != asset.IsSelected)
                {
                    drawModel.OnSelectedButtonPressed?.Invoke(asset);
                }

                x += elementWidth + UISettings.AssetHeaderSpace;

                elementWidth = UISettings.RemoveButtonWidth;
                var removeRect = new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight);
                if (GUI.Button(removeRect, "Remove"))
                {
                    drawModel.OnRemoveButtonPressed?.Invoke(asset);
                }

                x += elementWidth + UISettings.AssetHeaderSpace;

                elementWidth = UISettings.BaseButtonWidth;
                var comporandRect = new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight);
                if (GUI.Button(comporandRect, asset.IsBaseObject ? "Base" : "Theirs"))
                {
                    drawModel.OnComparandButtonPressed?.Invoke(asset);
                }

                x += elementWidth + UISettings.AssetHeaderSpace;

                elementWidth = UISettings.DiffButtonWidth;
                var diffRect = new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight);
                if (GUI.Button(diffRect, "Diff"))
                {
                    drawModel.OnDiffButtonPressed?.Invoke(asset);
                }

                x += elementWidth + UISettings.AssetHeaderSpace;
            }

            elementWidth = UISettings.AssetHeaderHeight;
            asset.IsFoldout = EditorGUI.Foldout(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight), asset.IsFoldout, string.Empty);
            x += elementWidth + UISettings.AssetHeaderSpace;

            if (IsFullScreenMode)
            {
                elementWidth = UISettings.AssetHeaderObjectWidth;
            }
            else
            {
                elementWidth = rect.width - x - UISettings.AssetHeaderSpace - UISettings.AssetHeaderDependencyCountWidth; 
            }

            var objectFieldRect = new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight);
            var assetState = drawModel?.GetAssetStateText?.Invoke(asset);
            var objectColor = assetState?.Item2 ?? GUI.color;

            EGuiKit.Color(objectColor, () =>
            {
                EditorGUI.ObjectField(objectFieldRect, new GUIContent(string.Empty, asset.Path), asset.Object, typeof(Object), asset.Object);
            });

            DrawContextMenu(asset, objectFieldRect);
            x += elementWidth + UISettings.AssetHeaderSpace;

            if (IsFullScreenMode)
            {
                if (ShowPath)
                {
                    elementWidth = EditorStyles.foldout.CalcSize(new GUIContent(asset.Path)).x;
                    EditorGUI.LabelField(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight), asset.Path);
                    x += elementWidth + UISettings.AssetHeaderSpace;
                }
            }
            else
            {
                elementWidth = UISettings.AssetHeaderDependencyCountWidth;
                EditorGUI.TextArea(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight),
                    !CountHiddenDependencies
                        ? asset.Dependencies.Count(IsDependencyAssetVisible).ToString()
                        : asset.Dependencies?.Count.ToString());

                x += elementWidth + UISettings.AssetHeaderSpace;

                return UISettings.AssetHeaderHeightWithPadding;
            }

            var leftWidth = rect.width - x;
            var neededWidthForGuid = UISettings.CommonGuidWidth + UISettings.CommonGuidTextWidth;

            if (leftWidth > neededWidthForGuid)
            {
                elementWidth = UISettings.CommonGuidWidth;
                x = rect.width - elementWidth;
                EditorGUI.TextArea(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight), asset.Guid);

                elementWidth = UISettings.CommonGuidTextWidth;
                x -= elementWidth;
                EditorGUI.LabelField(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight), "GUID:");
            }

            var neededWidthForDependency = neededWidthForGuid + UISettings.AssetHeaderSpace;
            if (ShowDependenciesCount)
            {
                neededWidthForDependency = neededWidthForGuid + UISettings.AssetHeaderDependencyCountWidth + UISettings.AssetHeaderDependencyCountTextWidth + UISettings.AssetHeaderSpace;
                if (leftWidth > neededWidthForDependency)
                {
                    elementWidth = UISettings.AssetHeaderDependencyCountWidth;
                    x -= elementWidth + UISettings.AssetHeaderSpace;
                    EditorGUI.TextArea(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight),
                        !CountHiddenDependencies
                            ? asset.Dependencies?.Count(IsDependencyAssetVisible).ToString()
                            : asset.Dependencies?.Count.ToString());

                    elementWidth = UISettings.AssetHeaderDependencyCountTextWidth;
                    x -= elementWidth;
                    EditorGUI.LabelField(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight), "Dependencies:");
                }
            }

            var neededWidthForSize = neededWidthForDependency + UISettings.AssetHeaderSpace;
            if (ShowSize)
            {
                neededWidthForSize = neededWidthForDependency + UISettings.AssetHeaderSizeWidth + UISettings.AssetHeaderSizeTextWidth + UISettings.AssetHeaderSpace;
                if (leftWidth > neededWidthForSize)
                {
                    elementWidth = UISettings.AssetHeaderSizeWidth;
                    x -= elementWidth + UISettings.AssetHeaderSpace;
                    EditorGUI.TextArea(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight), asset.ReadableSize ?? "");

                    elementWidth = UISettings.AssetHeaderSizeTextWidth;
                    x -= elementWidth;
                    EditorGUI.LabelField(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight), new GUIContent("Size:", drawModel?.GetSizeTooltipText?.Invoke(asset)));
                }
            }

            var neededWidthForState = neededWidthForSize + UISettings.DependencyStateTextAreaWidth + UISettings.AssetHeaderSpace;
            if (leftWidth > neededWidthForState)
            {
                if (drawModel?.DrawState ?? true)
                {
                    var message = drawModel?.GetAssetStateText?.Invoke(asset);
                    if (message.HasValue)
                    {
                        elementWidth = UISettings.DependencyStateTextAreaWidth;
                        x -= elementWidth + UISettings.AssetHeaderSpace;

                        EGuiKit.Color(message.Value.Item2,
                            () =>
                            {
                                EditorGUI.TextArea(new Rect(x, y, elementWidth, UISettings.AssetHeaderHeight), message.Value.Item1);
                            });
                    }
                }
            }

            return UISettings.AssetHeaderHeightWithPadding;
        }

        private float CalculateEmptyDependencyHeight(Asset mainAsset, DrawModel drawModel)
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

            return UISettings.DependencyHeightWithPadding;
        }

        private float TryDrawEmptyDependency(ref float x, ref float y, float width, Asset mainAsset, DrawModel drawModel)
        {
            if (CalculateEmptyDependencyHeight(mainAsset, drawModel) == 0.0f)
            {
                return 0.0f;
            }

            var result = DrawEmptyDependency(new Rect(x, y, width, UISettings.DependencyHeightWithPadding), drawModel.GetEmptyAssetText(mainAsset) ?? "None");
            y += result;
            return result;
        }

        private float DrawEmptyDependency(Rect rect, string text)
        {
            EditorGUI.DrawRect(rect, UISettings.RectBoxColor);
            EGuiKit.Color(UISettings.ErrorColor,
                () =>
                {
                    EditorGUI.LabelField(new Rect(rect.x + UISettings.DependencyFirstElementIndent, rect.y, rect.width, rect.height), text);
                });
            return UISettings.DependencyHeightWithPadding;
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

            return UISettings.DependencyHeightWithPadding;
        }

        private float TryDrawDependency(ref float x, ref float y, float width, Asset asset,
            Asset mainAsset, ToolBase.DrawModel drawModel)
        {
            if (CalculateDependencyHeight(asset, mainAsset) == 0.0f)
            {
                return 0.0f;
            }

            var result = DrawDependency(new Rect(x, y, width, UISettings.DependencyHeightWithPadding), asset, mainAsset, drawModel);
            y += result;
            return result;
        }

        private float DrawDependency(Rect rect, Asset asset, Asset mainAsset, DrawModel drawModel)
        {
            EditorGUI.DrawRect(rect, UISettings.RectBoxColor);

            var x = rect.x + UISettings.DependencyFirstElementIndent;

            var elementWidth = 0.0f;

            if (IsFullScreenMode)
            {
                elementWidth = UISettings.AssetHeaderObjectWidth;
            }
            else
            {
                elementWidth = rect.width
                               - UISettings.DependencyFirstElementIndent
                               - 3 * (UISettings.DependencyHeight + UISettings.DependencySpace) - UISettings.DependencySpace;
            }

            var objectFieldRect = new Rect(x, rect.y, elementWidth, UISettings.DependencyHeight);
            var assetState = drawModel?.GetAssetStateText?.Invoke(asset);
            var objectColor = assetState?.Item2 ?? GUI.color;

            EGuiKit.Color(objectColor, () =>
            {
                EditorGUI.ObjectField(objectFieldRect, asset.Object, typeof(Object), asset.Object);
            });

            DrawContextMenu(asset, objectFieldRect, mainAsset);
            x += elementWidth + UISettings.DependencySpace;

           elementWidth = UISettings.DependencyHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, UISettings.DependencyHeight),
                    EditorGUIUtility.IconContent(UISettings.HierarchyIconName)))
            {
                FindInHierarchyWindow(asset);
            }

            x += elementWidth + UISettings.DependencySpace;
            elementWidth = UISettings.DependencyHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, UISettings.DependencyHeight),
                    EditorGUIUtility.IconContent(UISettings.InspectorIconName)))
            {
                OpenProperty(asset);
            }

            x += elementWidth + UISettings.DependencySpace;

            if (IsFullScreenMode)
            {
                elementWidth = UISettings.CommonGuidTextWidth;
                EditorGUI.LabelField(new Rect(x, rect.y, elementWidth, UISettings.DependencyHeight), "GUID:");
                x += elementWidth;

                elementWidth = UISettings.CommonGuidWidth;
                EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, UISettings.DependencyHeight), asset.Guid);
                x += elementWidth + UISettings.DependencySpace;
            }

            elementWidth = UISettings.DependencyHeight;
            if (GUI.Button(new Rect(x, rect.y, elementWidth, UISettings.DependencyHeight),
                    EditorGUIUtility.IconContent(UISettings.FolderIconName)))
            {
                if (!string.IsNullOrEmpty(asset.Path))
                {
                    EditorUtility.RevealInFinder(asset.Path);
                }
            }

            if (IsFullScreenMode)
            {
                x += elementWidth + UISettings.DependencySpace;
                elementWidth = 40.0f;
                EditorGUI.LabelField(new Rect(x, rect.y, elementWidth, UISettings.DependencyHeight), "Path:");
                x += elementWidth;

                var drawState = (drawModel?.DrawState ?? false) && (asset.DiffState != AssetDiffState.None || asset.MetaDiffState != AssetDiffState.None);
                if (drawState)
                {
                    elementWidth = rect.width - x - UISettings.DependencyStateTextAreaWidth;
                }
                else
                {
                    elementWidth = rect.width - x;
                }

                EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, UISettings.DependencyHeight), asset.Path);
                x += elementWidth;

                if (drawState)
                {
                    var message = drawModel?.GetAssetStateText?.Invoke(asset);
                    if (message.HasValue)
                    {
                        elementWidth = UISettings.DependencyStateTextAreaWidth;
                        EGuiKit.Color(message.Value.Item2,
                            () =>
                            {
                                EditorGUI.TextArea(new Rect(x, rect.y, elementWidth, UISettings.DependencyHeight), message.Value.Item1);
                            });
                    }
                }
            }
            return UISettings.DependencyHeightWithPadding;
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

        protected virtual Object DrawSelectedObject(Object obj, Action<Object> onNewObject = null)
        {
            return EGuiKit.Object(obj, typeof(Object), true, onNewObject, GUILayout.Height(UISettings.AssetHeaderHeight));
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
        private string GetEmptyCollectionText()
        {
            return "Nothing to show.";
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
                string.Format(UISettings.SceneHierarchySearchReferenceFormat, asset.Path),
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