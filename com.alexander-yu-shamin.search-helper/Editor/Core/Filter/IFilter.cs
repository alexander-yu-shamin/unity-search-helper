using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Toolkit.Runtime.Extensions;
using UnityEngine;

namespace SearchHelper.Editor.Core.Filter
{
    public enum FilterRuleMode
    {
        Include,
        Exclude
    }

    public interface IFilter
    {
        public bool IsAllowed(Asset context, Asset parent = null);
    }

    [Serializable]
    public class FilterRule
    {
        [SerializeField] private FilterRuleMode _mode;
        public FilterRuleMode Mode => _mode;

        [SerializeField] private AssetTarget _target;
        public AssetTarget Target => _target;

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
        public AssetTarget Target { get; }
        public Regex Regex { get; }

        public bool IsMatch(Asset context)
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

}
