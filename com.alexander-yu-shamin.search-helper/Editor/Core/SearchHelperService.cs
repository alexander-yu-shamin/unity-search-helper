#define SEARCH_HELPER_ENABLE_CACHING
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor.Core
{
    public class SearchHelperService : AssetPostprocessor
    {
        private const string ObjectSearchFilter = "t:Object";
        public static event Action<string[], string[], string[], string[]> OnAssetChanged;
#if SEARCH_HELPER_ENABLE_CACHING
        private static Dictionary<string, List<ObjectContext>> DependencyMap { get; set; } = new Dictionary<string, List<ObjectContext>>();
        private static bool HasOnlyNames { get; set; } = false;
        private static bool HasFullDependencyMap { get; set; } = false;
#endif

        public static IEnumerable<string> FindAssetPaths(string root = null)
        {
            AssetDatabase.SaveAssets();
#if SEARCH_HELPER_ENABLE_CACHING
#if SEARCH_HELPER_ENABLE_STOPWATCH
            var stopwatch = StartStopwatch();
#endif
            if (root == null && DependencyMap != null && HasOnlyNames)
            {
                return DependencyMap.Keys.ToList();
            }
            
#if SEARCH_HELPER_ENABLE_STOPWATCH
            StopStopwatch("FindAssetsPath::GetFromCache", stopwatch);
#endif
#endif

#if SEARCH_HELPER_ENABLE_STOPWATCH
            stopwatch = StartStopwatch();
#endif
            var assets = FindAssets(ObjectSearchFilter, root).Select(AssetDatabase.GUIDToAssetPath)
                                                             .Where(path => !string.IsNullOrEmpty(path));
#if SEARCH_HELPER_ENABLE_STOPWATCH
            StopStopwatch("FindAssetsPath::FindAssets", stopwatch);
#endif

#if SEARCH_HELPER_ENABLE_CACHING

#if SEARCH_HELPER_ENABLE_STOPWATCH
            stopwatch = StartStopwatch();
#endif
            if (root == null && !assets.IsNullOrEmpty())
            {
                HasOnlyNames = true;
                DependencyMap = assets.ToDictionary(k => k, v => null as List<ObjectContext>);
            }

#if SEARCH_HELPER_ENABLE_STOPWATCH
            StopStopwatch("FindAssetsPath::Caching", stopwatch);
#endif
#endif
            return assets;
        }

        public static IEnumerable<Object> FindAssetObjects(string root = null)
        {
            AssetDatabase.SaveAssets();

#if SEARCH_HELPER_ENABLE_STOPWATCH
            var stopwatch = StartStopwatch();
#endif
            var assetObjects = FindAssets(ObjectSearchFilter, root).Select(AssetDatabase.GUIDToAssetPath)
                                                                   .Select(AssetDatabase.LoadMainAssetAtPath)
                                                                   .Where(asset => asset != null);
#if SEARCH_HELPER_ENABLE_STOPWATCH
            StopStopwatch("FindAssetObjects", stopwatch);
#endif
            return assetObjects;
        }

        public static IEnumerable<string> FindAssets(string searchFilter, string root = null)
        {
            return AssetDatabase.FindAssets(searchFilter, GetSearchDirs(root));
        }

        public static ObjectContext FindUsedBy(Object obj, bool useCache = true)
        {
            if (obj == null)
            {
                return null;
            }

            AssetDatabase.SaveAssets();

            var searchedCtx = ObjectContext.ToObjectContext(obj);

#if SEARCH_HELPER_ENABLE_CACHING
            if (useCache && (DependencyMap?.ContainsKey(searchedCtx.Path) ?? false))
            {
#if SEARCH_HELPER_ENABLE_STOPWATCH
                var stopwatchCaching = StartStopwatch();
#endif
                var dependencies = DependencyMap[searchedCtx.Path];
                if (dependencies != null)
                {
                    searchedCtx.Dependencies = dependencies;
#if SEARCH_HELPER_ENABLE_STOPWATCH
                    StopStopwatch("FindUsedBy::GetFromCache", stopwatchCaching);
#endif
                    return searchedCtx;
                }
#if SEARCH_HELPER_ENABLE_STOPWATCH
                StopStopwatch("FindUsedBy::GetFromCache", stopwatchCaching);
#endif
            }
#endif
            var paths = SearchHelperService.FindAssetPaths();
            if (!paths.Any())
            {
                return null;
            }

#if SEARCH_HELPER_ENABLE_STOPWATCH
            var stopwatch = StartStopwatch();
#endif
            foreach (var path in paths)
            {
                if (path == searchedCtx.Path)
                {
                    continue;
                }

                var dependencies = AssetDatabase.GetDependencies(path);
                if (dependencies.ToHashSet().Contains(searchedCtx.Path))
                {
                    searchedCtx.Dependencies.Add(ObjectContext.FromPath(path));
                }
            }

#if SEARCH_HELPER_ENABLE_STOPWATCH
            StopStopwatch("FindUsedBy::GetDependencies", stopwatch);
#endif

#if SEARCH_HELPER_ENABLE_CACHING

#if SEARCH_HELPER_ENABLE_STOPWATCH
            stopwatch = StartStopwatch();
#endif

            CacheMainObject(searchedCtx.Path, searchedCtx.Dependencies);

#if SEARCH_HELPER_ENABLE_STOPWATCH
            StopStopwatch("FindUsedBy::Caching", stopwatch);
#endif
#endif
            return searchedCtx;
        }

        public static ObjectContext FindDependencies(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            AssetDatabase.SaveAssets();

            var dependencies = ObjectContext.ToObjectContexts(EditorUtility.CollectDependencies(new[] { obj }), obj);
            var path = AssetDatabase.GetAssetPath(obj);
            var guid = string.Empty;
            var isFolder = false;

            if (!string.IsNullOrEmpty(path))
            {
                guid = AssetDatabase.AssetPathToGUID(path);
                isFolder = AssetDatabase.IsValidFolder(path);
            }

            var objectContext = new ObjectContext()
            {
                Object = obj,
                Path = path,
                Guid = guid,
                IsFolder = isFolder,
                Dependencies = dependencies.ToList()
            };

            return objectContext;
        }

        public static Dictionary<string, List<ObjectContext>> BuildDependencyMap(string root = null, bool useCache = true)
        {
            AssetDatabase.SaveAssets();

#if SEARCH_HELPER_ENABLE_CACHING
            if (useCache && DependencyMap != null && HasFullDependencyMap)
            {
#if SEARCH_HELPER_ENABLE_STOPWATCH
                var stopwatchCaching = StartStopwatch();
#endif
                var DependencyMapCopy = DependencyMap.ToDictionary(k => k.Key, v => v.Value);
#if SEARCH_HELPER_ENABLE_STOPWATCH
                StopStopwatch("BuildDependencyMap", stopwatchCaching);
#endif
                return DependencyMapCopy;
            }
#endif


#if SEARCH_HELPER_ENABLE_STOPWATCH
            var stopwatch = StartStopwatch();
#endif
            var result = new Dictionary<string, List<ObjectContext>>();
            var paths = SearchHelperService.FindAssetPaths(root);
            foreach (var path in paths)
            {
                var dependencies = AssetDatabase.GetDependencies(path);
                foreach (var dependency in dependencies)
                {
                    if (path == dependency)
                    {
                        continue;
                    }

                    var context = ObjectContext.FromPath(path);
#if SEARCH_HELPER_ENABLE_CACHING
                    CacheDependencyObject(context, dependency);
#endif
                    if (result.ContainsKey(dependency))
                    {
                        result[dependency].Add(context);
                    }
                    else
                    {
                        result.Add(dependency, new List<ObjectContext>() { context });
                    }
                }
            }

#if SEARCH_HELPER_ENABLE_CACHING
            if (root == null)
            {
                DependencyMap = result;
                HasFullDependencyMap = paths?.Count() == DependencyMap?.Count;
            }
            else
            {
                if (DependencyMap == null)
                {
                    DependencyMap = result;
                }
                else
                {
                    foreach(var (path, contexts) in result)
                    {
                        CacheMainObject(path, contexts);
                    }
                }
            }
#endif

#if SEARCH_HELPER_ENABLE_STOPWATCH
            StopStopwatch("BuildDependencyMap::BuildMap", stopwatch);
#endif
            return result;
        }

        public static Object FindObjectByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadMainAssetAtPath(path);
        }

        public static string GetObjectGuid(Object obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                return "Object has no GUID";
            }

            return AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets);
        }
        private static string[] GetSearchDirs(string root = null)
        {
            var searchDirs = new[] { "Assets" };
            if (!string.IsNullOrEmpty(root))
            {
                searchDirs = new[] { root };
            }

            return searchDirs;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload)
            {
                return;
            }

            if (!importedAssets.IsNullOrEmpty()
                || !deletedAssets.IsNullOrEmpty()
                || !movedAssets.IsNullOrEmpty()
                || !movedFromAssetPaths.IsNullOrEmpty())
            {
#if SEARCH_HELPER_ENABLE_CACHING
                ClearDependencyMap();
#endif
                OnAssetChanged?.Invoke(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            }
        }

        public static string GetFileHashMD5(ref MD5 md5, string path)
        {
            var hashBytes = md5.ComputeHash(File.ReadAllBytes(path));
            var hash = BitConverter.ToString(hashBytes);
            return hash;
        }

        public static string GetFileHashSHA256(string path, int skipLines)
        {
            using var sha = SHA256.Create();
            using var reader = new StreamReader(path, Encoding.UTF8);

            for (int i = 0; i < skipLines; i++)
            {
                reader.ReadLine();
            }

            var remaining = reader.ReadToEnd();
            var bytes = Encoding.UTF8.GetBytes(remaining);
            var hash = sha.ComputeHash(bytes);

            return Convert.ToBase64String(hash);
        }

#if SEARCH_HELPER_ENABLE_CACHING
        private static void ClearDependencyMap()
        {
            HasOnlyNames = false;
            HasFullDependencyMap = false;
            DependencyMap = new Dictionary<string, List<ObjectContext>>();
        }

        private static void CacheMainObject(string main, List<ObjectContext> dependencies = null)
        {
            if (DependencyMap.ContainsKey(main))
            {
                DependencyMap[main] = dependencies;
            }
            else
            {
                DependencyMap[main] = dependencies;
            }
        }

        [Conditional("SEARCH_HELPER_ENABLE_CACHING")]
        private static void CacheDependencyObject(ObjectContext dependency, string main)
        {
            DependencyMap.TryAdd(main, new List<ObjectContext>());
            if (DependencyMap[main] == null)
            {
                DependencyMap[main] = new List<ObjectContext>() { dependency };
            }
            else
            {
                DependencyMap[main].Add(dependency);
            }
        }
#endif

        private static Stopwatch StartStopwatch()
        {
            return Stopwatch.StartNew();
        }

        private static void StopStopwatch(string message, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            UnityEngine.Debug.Log($"{message}: {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}