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

    public class SearchHelperSortManager
    {
        public SortVariant CurrentSortVariant { get; private set; } = SortVariant.NoSorting;
        public bool ShouldMainObjectsBeSorted { get; private set; }
        public HashSet<SortVariant> PossibleSortVariants { get; private set; }
        private Action OnFilterChanged { get; set; }

        public SearchHelperSortManager(Action onFilterChanged)
        {
            PossibleSortVariants = new HashSet<SortVariant>(Enum.GetValues(typeof(SortVariant)).Cast<SortVariant>());
            OnFilterChanged = onFilterChanged;
        }

        public bool Sort(IEnumerable<ObjectContext> data)
        {
            if (data.IsNullOrEmpty())
            {
                return false;
            }

            if (CurrentSortVariant == SortVariant.NoSorting)
            {
                return true;
            }

            if (ShouldMainObjectsBeSorted)
            {
                if (data is List<ObjectContext> list)
                {
                    SortInPlace(list, CurrentSortVariant);
                }
            }

            foreach (var context in data.Where(context => !context.Dependencies.IsNullOrEmpty()))
            {
                if (context.Dependencies is List<ObjectContext> list)
                {
                    SortInPlace(list, CurrentSortVariant);
                }
                else
                {
                    context.Dependencies = OrderBy(context.Dependencies, CurrentSortVariant).ToList();
                }
            }

            return true;
        }

        public void Select(SortVariant sortVariant)
        {
            CurrentSortVariant = sortVariant;
            OnFilterChanged?.Invoke();
        }

        private IEnumerable<ObjectContext> OrderBy(IEnumerable<ObjectContext> objectContexts, SortVariant sortVariant)
        {
            switch (sortVariant)
            {
                case SortVariant.ByName:
                    return objectContexts.OrderBy(el => el.Object?.name);
                case SortVariant.ByPath:
                    return objectContexts.OrderBy(el => el.Path);
                case SortVariant.Natural:
                    return objectContexts.OrderBy(el => el.Object?.name,
                        Comparer<string>.Create(EditorUtility.NaturalCompare));
                case SortVariant.ByCount:
                    return objectContexts.OrderByDescending(el => el.Dependencies.Count);
                case SortVariant.NoSorting:
                default:
                    return objectContexts;
            }
        }

        private void SortInPlace(List<ObjectContext> list, SortVariant sortVariant)
        {
            switch (sortVariant)
            {
                case SortVariant.ByName:
                    list.Sort((a, b) => string.Compare(a.Object?.name, b.Object?.name, StringComparison.Ordinal));
                    break;
                case SortVariant.ByPath:
                    list.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
                    break;
                case SortVariant.Natural:
                    list.Sort((a, b) => EditorUtility.NaturalCompare(a.Object?.name, b.Object?.name));
                    break;
                case SortVariant.ByCount:
                    list.Sort((a, b) => b.Dependencies.Count.CompareTo(a.Dependencies.Count));
                    break;
                case SortVariant.NoSorting:
                default:
                    break;
            }
        }
    }
}