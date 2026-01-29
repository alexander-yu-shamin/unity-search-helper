using System;

namespace SearchHelper.Editor.Core.Filter
{
    public class FilterByStringManager : ObservableData, IFilter
    {
        public AssetTarget CurrentFilterByStringTarget { get; private set; } = AssetTarget.Path;
        public FilterRuleMode CurrentFilterByStringMode { get; private set; } = FilterRuleMode.Include;
        public string CurrentFilterString { get; private set; }

        public AssetTarget[] PossibleAssetTargets { get; private set; } = new[]
        {
            AssetTarget.Path,
            AssetTarget.Name,
            AssetTarget.Type,
        };

        public FilterRuleMode[] PossibleFilterRuleModes { get; private set; } = new[]
        {
            FilterRuleMode.Include,
            FilterRuleMode.Exclude,
        };

        public bool IsAllowed(Asset context, Asset parent = null)
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

        public void SelectFilterByString(AssetTarget target, FilterRuleMode mode, string filter)
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
                OnDataChanged();
            }
        }
    }
}
