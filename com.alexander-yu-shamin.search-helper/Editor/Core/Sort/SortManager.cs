using System;
using System.Collections.Generic;
using System.Linq;
using Toolkit.Runtime.Extensions;
using UnityEditor;

namespace SearchHelper.Editor.Core.Sort
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

    public class SortManager : ObservableData
    {
        public SortVariant CurrentSortVariant { get; private set; } = SortVariant.NoSorting;
        public SortOrder CurrentSortOrder { get; private set; } = SortOrder.Descending;

        private bool _sortMainAssets = true;

        public bool SortMainAssets
        {
            get => _sortMainAssets;
            set
            {
                _sortMainAssets = value;
                OnDataChanged();
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
        
        public bool Sort(IEnumerable<Asset> data)
        {
            if (CurrentSortVariant == SortVariant.NoSorting)
            {
                return true;
            }

            if (data.IsNullOrEmpty())
            {
                return false;
            }

            if (SortMainAssets)
            {
                if (data is List<Asset> list)
                {
                    SortInPlace(list, CurrentSortVariant, CurrentSortOrder);
                }
            }

            foreach (var context in data.Where(context => !context.Dependencies.IsNullOrEmpty()))
            {
                if (context.Dependencies is List<Asset> list)
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
            OnDataChanged();
        }

        public void Select(SortOrder sortOrder)
        {
            if (CurrentSortOrder == sortOrder)
            {
                return;
            }

            CurrentSortOrder = sortOrder;
            OnDataChanged();
        }

        private void SortInPlace(List<Asset> list, SortVariant sortVariant, SortOrder sortOrder)
        {
            if (sortVariant == SortVariant.NoSorting)
            {
                return;
            }

            var target = ToTarget(sortVariant);

            Comparison<Asset> comparison = sortVariant switch
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

        private AssetTarget ToTarget(SortVariant sortVariant)
        {
            return sortVariant switch
            {
                SortVariant.ByName                           => AssetTarget.Name,
                SortVariant.ByPath                           => AssetTarget.Path,
                SortVariant.Natural                          => AssetTarget.Path,
                SortVariant.NoSorting or SortVariant.ByCount => AssetTarget.NoTarget,
                _                                            => AssetTarget.NoTarget
            };
        }
    }
}