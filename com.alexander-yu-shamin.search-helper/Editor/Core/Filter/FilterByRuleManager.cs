using System.Collections.Generic;
using System.IO;
using System.Linq;
using Toolkit.Editor.Helpers.AssetDatabase;
using Toolkit.Runtime.Extensions;
using UnityEngine;

namespace SearchHelper.Editor.Core.Filter
{
    public class FilterByRuleManager : ObservableData, IFilter
    {
        public ResourceData<SearchHelperFilterRules> CurrentFilterRule { get; private set; }
        public List<ResourceData<SearchHelperFilterRules>> FilterRules { get; private set; } = new();
        private List<CompiledFilterRule> CompiledFilterRules { get; set; }

        public bool IsAllowed(Asset context, Asset parent = null)
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

            CurrentFilterRule.Data.DataChanged += OnDataChanged;
            OnDataChanged();
        }

        public void UnselectFilterRule()
        {
            if (CurrentFilterRule != null && CurrentFilterRule.Data != null)
            {
                CurrentFilterRule.Data.DataChanged -= OnDataChanged;
            }

            CurrentFilterRule = null;
            CompiledFilterRules = null;
            OnDataChanged();
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
    }
}