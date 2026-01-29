using System;
using System.Collections.Generic;
using SearchHelper.Editor.Core;
using UnityEngine;

namespace SearchHelper.Editor
{
    [CreateAssetMenu(fileName = "Filter Rules", menuName = "Scriptable Objects/SearchHelper/Filter Rules")]
    public class SearchHelperFilterRules : ScriptableObject, IDataObserver
    {
        public event Action DataChanged;

        public List<FilterRule> FilterRules = new();

        private void OnValidate()
        {
            DataChanged?.Invoke();
        }
    }
}
