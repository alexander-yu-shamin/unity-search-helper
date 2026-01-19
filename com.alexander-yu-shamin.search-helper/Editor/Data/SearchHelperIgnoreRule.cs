using System;
using System.Collections.Generic;
using UnityEngine;

namespace SearchHelper.Editor
{
    [CreateAssetMenu(fileName = "ignored files", menuName = "Scriptable Objects/SearchHelper/Ignored Files")]
    public class SearchHelperIgnoreRule : ScriptableObject
    {
        public event Action OnDataChanged;

        public List<string> IgnoredNames;
        public List<string> IgnoredPaths;
        public List<string> IgnoredTypes;

        private void OnValidate()
        {
            OnDataChanged?.Invoke();
        }
    }
}
