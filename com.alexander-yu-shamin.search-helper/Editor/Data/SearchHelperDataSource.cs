using System.Collections.Generic;
using System.Linq;
using Toolkit.Runtime.Extensions;
using Unity.Android.Types;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SearchHelper.Editor.Data
{
    public class DataDescription<T>
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public T Data { get; set; }
    }

    public static class SearchHelperDataSource
    {
        private static string UnusedPatternSearchFilter = $"t:{nameof(SearchHelperIgnoreRule)}";

        public static List<DataDescription<SearchHelperIgnoreRule>> GetAllUnusedPatterns()
        {
            AssetDatabase.Refresh();
            var guids = AssetDatabase.FindAssets(UnusedPatternSearchFilter);
            if (guids.IsNullOrEmpty())
            {
                return null;
            }

            var result = new List<DataDescription<SearchHelperIgnoreRule>>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var pattern = AssetDatabase.LoadAssetAtPath<SearchHelperIgnoreRule>(path);
                result.Add(new DataDescription<SearchHelperIgnoreRule>
                {
                    Path = path, Data = pattern
                });
            }

            return result;
        }
    }
}
