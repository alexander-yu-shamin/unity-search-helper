using System;
using System.Collections.Generic;
using System.Linq;
using Toolkit.Runtime.Extensions;
using UnityEditor;

namespace SearchHelper.Editor.Core
{
    public enum SortVariant
    {
        NoSorting = 0,
        ByName,
        ByPath,
        ByCount,
        Natural
    }

    public enum SortOrder
    {
        Ascending,
        Descending
    }

    public class SearchHelperSortManager
    {
        public SortVariant CurrentSortVariant { get; private set; } = SortVariant.NoSorting;
        public SortOrder CurrentSortOrder { get; private set; } = SortOrder.Descending;

        private bool _shouldBeMainObjectBeSorted = true;

        public bool ShouldMainObjectsBeSorted
        {
            get => _shouldBeMainObjectBeSorted;
            set
            {
                _shouldBeMainObjectBeSorted = value;
                UpdateData();
            }
        }

        public SortVariant[] PossibleSortVariants { get; private set; } = new[]
        {
            SortVariant.NoSorting,
            SortVariant.ByName,
            SortVariant.ByPath,
            SortVariant.ByCount,
            SortVariant.Natural
        };

        public SortOrder[] PossibleSortOrders { get; private set; } = new[]
        {
            SortOrder.Descending,
            SortOrder.Ascending
        };
        
        private Action OnFilterChanged { get; set; }

        public SearchHelperSortManager(Action onFilterChanged)
        {
            OnFilterChanged = onFilterChanged;
        }

        public bool Sort(IEnumerable<ObjectContext> data)
        {
            if (CurrentSortVariant == SortVariant.NoSorting)
            {
                return true;
            }

            if (data.IsNullOrEmpty())
            {
                return false;
            }

            if (ShouldMainObjectsBeSorted)
            {
                if (data is List<ObjectContext> list)
                {
                    SortInPlace(list, CurrentSortVariant, CurrentSortOrder);
                }
            }

            foreach (var context in data.Where(context => !context.Dependencies.IsNullOrEmpty()))
            {
                if (context.Dependencies is List<ObjectContext> list)
                {
                    SortInPlace(list, CurrentSortVariant, CurrentSortOrder);
                }
            }

            return true;
        }

        public void Select(SortVariant sortVariant)
        {
            if (CurrentSortVariant == sortVariant)
            {
                return;
            }

            CurrentSortVariant = sortVariant;
            UpdateData();
        }

        public void Select(SortOrder sortOrder)
        {
            if (CurrentSortOrder == sortOrder)
            {
                return;
            }

            CurrentSortOrder = sortOrder;
            UpdateData();
        }

        private void UpdateData()
        {
            OnFilterChanged?.Invoke();
        }

        private void SortInPlace(List<ObjectContext> list, SortVariant sortVariant, SortOrder sortOrder)
        {
            if (sortVariant == SortVariant.NoSorting)
            {
                return;
            }

            var target = ToTarget(sortVariant);

            Comparison<ObjectContext> comparison = sortVariant switch
            {
                SortVariant.ByName or SortVariant.ByPath => (a, b) =>
                    string.CompareOrdinal(a.GetTarget(target), b.GetTarget(target)),

                SortVariant.ByCount => (a, b) => a.Dependencies.Count.CompareTo(b.Dependencies.Count),

                SortVariant.Natural => (a, b) => EditorUtility.NaturalCompare(a.GetTarget(target), b.GetTarget(target)),

                _ => null
            };

            if (comparison == null)
            {
                return;
            }

            if (sortOrder == SortOrder.Descending)
            {
                var baseComparison = comparison;
                comparison = (a, b) => baseComparison(b, a);
            }

            list.Sort(comparison);
        }

        private ObjectContextTarget ToTarget(SortVariant sortVariant)
        {
            return sortVariant switch
            {
                SortVariant.ByName                           => ObjectContextTarget.Name,
                SortVariant.ByPath                           => ObjectContextTarget.Path,
                SortVariant.Natural                          => ObjectContextTarget.Path,
                SortVariant.NoSorting or SortVariant.ByCount => ObjectContextTarget.NoTarget,
                _                                            => ObjectContextTarget.NoTarget
            };
        }
    }
}