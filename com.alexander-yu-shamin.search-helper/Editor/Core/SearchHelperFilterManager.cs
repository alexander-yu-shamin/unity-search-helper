using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Toolkit.Editor.Helpers.AssetDatabase;
using Toolkit.Runtime.Extensions;
using UnityEngine;

namespace SearchHelper.Editor.Core
{
    public enum FilterRuleMode
    {
        Include,
        Exclude
    }

    [Serializable]
    public class FilterRule
    {
        [SerializeField] private FilterRuleMode _mode;
        public FilterRuleMode Mode => _mode;

        [SerializeField] private ObjectContextTarget _target;
        public ObjectContextTarget Target => _target;

        [SerializeField] private List<string> _pattern;
        public List<string> Patterns => _pattern;

        public override string ToString()
        {
            return
                $"Mode: {Mode}; Target: {Target}; Patterns: {string.Join(" | ", Patterns.IsNullOrEmpty() ? string.Empty : Patterns)}";
        }
    }

    public class CompiledFilterRule
    {
        public FilterRuleMode Mode { get; }
        public ObjectContextTarget Target { get; }
        public Regex Regex { get; }

        public bool IsMatch(ObjectContext context)
        {
            if (Regex == null)
            {
                return false;
            }

            return Regex.IsMatch(context.GetTarget(Target));
        }

        public CompiledFilterRule(FilterRule rule)
        {
            Mode = rule.Mode;
            Target = rule.Target;
            if (!rule.Patterns.IsNullOrEmpty())
            {
                Regex = new Regex(string.Join("|", rule.Patterns), RegexOptions.Compiled);
            }
        }
    }

    public class SearchHelperFilterManager
    {
        #region FilterRule

        public ResourceData<SearchHelperFilterRules> CurrentFilterRule { get; private set; }
        public List<ResourceData<SearchHelperFilterRules>> FilterRules { get; private set; } = new();
        protected List<CompiledFilterRule> CompiledFilterRules { get; set; }

        #endregion

        #region FilterString

        public ObjectContextTarget CurrentFilterByStringTarget { get; private set; } = ObjectContextTarget.Path;
        public FilterRuleMode CurrentFilterByStringMode { get; private set; } = FilterRuleMode.Include;
        public string CurrentFilterString { get; private set; }

        public ObjectContextTarget[] PossibleObjectContextTargets { get; private set; } = new[]
        {
            ObjectContextTarget.Path,
            ObjectContextTarget.Name,
            ObjectContextTarget.Type,
        };

        public FilterRuleMode[] PossibleFilterRuleModes { get; private set; } = new[]
        {
            FilterRuleMode.Include,
            FilterRuleMode.Exclude,
        };
        #endregion

        protected Action OnFilterChanged { get; set; }

        public SearchHelperFilterManager(Action onFilterChanged)
        {
            OnFilterChanged = onFilterChanged;
        }

        public bool IsAllowed(ObjectContext context, ObjectContext parent = null)
        {
            if (!IsAllowedByFilterRules(context, parent))
            {
                return false;
            }

            return IsAllowedByFilterString(context, parent);
        }

        private bool IsAllowedByFilterString(ObjectContext context, ObjectContext parent)
        {
            if (!string.IsNullOrEmpty(CurrentFilterString))
            {
                var value = context.GetTarget(CurrentFilterByStringTarget);

                var matches = value != null
                              && value.Contains(CurrentFilterString, StringComparison.InvariantCultureIgnoreCase);

                if (CurrentFilterByStringMode == FilterRuleMode.Include)
                {
                    return matches;
                }

                return !matches;
            }

            return true;
        }

        private bool IsAllowedByFilterRules(ObjectContext context, ObjectContext parent)
        {
            if (!CompiledFilterRules.IsNullOrEmpty())
            {
                var hasIncludeRules = CompiledFilterRules.Any(r => r.Mode == FilterRuleMode.Include);
                var includedByRules = !hasIncludeRules;

                for (var i = 0; i < CompiledFilterRules.Count; i++)
                {
                    var rule = CompiledFilterRules[i];

                    if (!rule.IsMatch(context))
                    {
                        continue;
                    }

                    if (rule.Mode == FilterRuleMode.Exclude)
                    {
                        return false;
                    }

                    includedByRules = true;
                }

                if (!includedByRules)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateData()
        {
            OnFilterChanged?.Invoke();
        }

        #region FilterString

        public void SelectFilterByString(ObjectContextTarget target, FilterRuleMode mode, string filter)
        {
            var updated = false;

            if (CurrentFilterByStringTarget != target)
            {
                updated = true;
                CurrentFilterByStringTarget = target;
            }

            if (CurrentFilterByStringMode != mode)
            {
                updated = true;
                CurrentFilterByStringMode = mode;
            }

            if (CurrentFilterString != filter)
            {
                updated = true;
                CurrentFilterString = filter;
            }

            if (updated)
            {
                UpdateData();
            }
        }

        #endregion

        #region FilterRule

        public void SelectFilterRule(ResourceData<SearchHelperFilterRules> resource)
        {
            if (resource == null || resource.Data == null || resource.Data.FilterRules.IsNullOrEmpty())
            {
                return;
            }

            CurrentFilterRule = resource;
            CompiledFilterRules = new List<CompiledFilterRule>();

            foreach (var filterRule in resource.Data.FilterRules)
            {
                try
                {
                    CompiledFilterRules.Add(new CompiledFilterRule(filterRule));
                }
                catch
                {
                    Debug.LogError($"Cannot create filter rule for {filterRule}");
                }
            }

            CurrentFilterRule.Data.OnDataChanged += UpdateData;
            UpdateData();
        }

        public void UnselectFilterRule()
        {
            if (CurrentFilterRule != null && CurrentFilterRule.Data != null)
            {
                CurrentFilterRule.Data.OnDataChanged -= UpdateData;
            }

            CurrentFilterRule = null;
            CompiledFilterRules = null;
            UpdateData();
        }

        public void UpdateFilterRules()
        {
            FilterRules = LoadRulesFromDisk();
        }

        public void UpdateFilterRulesIfEmpty()
        {
            if (FilterRules.IsNullOrEmpty())
            {
                UpdateFilterRules();
            }
        }

        private List<ResourceData<SearchHelperFilterRules>> LoadRulesFromDisk()
        {
            var rules = AssetDatabaseKit.GetAssetResources<SearchHelperFilterRules>();
            UpdateRuleNames(rules);
            return rules;
        }

        private void UpdateRuleNames(List<ResourceData<SearchHelperFilterRules>> rules)
        {
            if (rules.IsNullOrEmpty())
            {
                return;
            }

            foreach (var rule in rules)
            {
                if (rule.Path.StartsWith("Packages"))
                {
                    rule.Name = "Default: "
                                + Path.GetFileName(Path.GetDirectoryName(rule.Path))
                                + "/"
                                + Path.GetFileNameWithoutExtension(rule.Path);
                    continue;
                }

                rule.Name = Path.GetFileName(Path.GetDirectoryName(rule.Path))
                            + "/"
                            + Path.GetFileNameWithoutExtension(rule.Path);
            }
        }

        #endregion
    }
}