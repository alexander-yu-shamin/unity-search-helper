#define SEARCH_HELPER_ENABLE_CACHING
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Toolkit.Editor.Helpers.Diagnostics;
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
        private static Dictionary<string, List<Asset>> DependencyMap { get; set; } = new Dictionary<string, List<Asset>>();
        private static bool HasOnlyNames { get; set; } = false;
        private static bool HasFullDependencyMap { get; set; } = false;
#endif

        public static IEnumerable<string> FindAssetPaths(string root = null)
        {
            using var measure = Profiler.Measure($"FindAssetPaths {root}");
            AssetDatabase.SaveAssets();
#if SEARCH_HELPER_ENABLE_CACHING
            if (root == null && DependencyMap != null && HasOnlyNames)
            {
                return DependencyMap.Keys.ToList();
            }
#endif
            var assets = FindAssets(ObjectSearchFilter, root).Select(AssetDatabase.GUIDToAssetPath)
                                                             .Where(path => !string.IsNullOrEmpty(path));
#if SEARCH_HELPER_ENABLE_CACHING
            if (root == null && !assets.IsNullOrEmpty())
            {
                using (Profiler.Measure($"FindAssetPaths:: Caching {root}"))
                {
                    HasOnlyNames = true;
                    DependencyMap = assets.ToDictionary(k => k, v => null as List<Asset>);
                }
            }
#endif
            return assets;
        }

        public static IEnumerable<Object> FindAssetObjects(string root = null)
        {
            AssetDatabase.SaveAssets();
            var assetObjects = FindAssets(ObjectSearchFilter, root).Select(AssetDatabase.GUIDToAssetPath)
                                                                   .Select(AssetDatabase.LoadMainAssetAtPath)
                                                                   .Where(asset => asset != null);
            return assetObjects;
        }

        public static IEnumerable<string> FindAssets(string searchFilter, string root = null)
        {
            using var measure = Profiler.Measure($"FindAssets {searchFilter} {root}");
            return AssetDatabase.FindAssets(searchFilter, GetSearchDirs(root));
        }

        public static Asset FindUsedBy(Object obj, bool useCache = true)
        {
            if (obj == null)
            {
                return null;
            }

            using var measure = Profiler.Measure($"FindUsedBy {obj.name}");

            AssetDatabase.SaveAssets();

            var searchedCtx = Asset.ToAsset(obj);

#if SEARCH_HELPER_ENABLE_CACHING
            if (useCache && (DependencyMap?.ContainsKey(searchedCtx.Path) ?? false))
            {
                using (Profiler.Measure($"FindUsedBy:: Caching {searchedCtx.Path}"))
                {
                    var dependencies = DependencyMap[searchedCtx.Path];
                    if (dependencies != null)
                    {
                        searchedCtx.Dependencies = dependencies;
                        return searchedCtx;
                    }
                }
            }
#endif
            var paths = SearchHelperService.FindAssetPaths();
            if (!paths.Any())
            {
                return null;
            }

            using (Profiler.Measure($"FindUsedBy:: GetDependencies {searchedCtx.Path}"))
            {
                foreach (var path in paths)
                {
                    if (path == searchedCtx.Path)
                    {
                        continue;
                    }

                    var dependencies = AssetDatabase.GetDependencies(path);
                    if (dependencies.ToHashSet().Contains(searchedCtx.Path))
                    {
                        searchedCtx.Dependencies.Add(Asset.FromPath(path));
                    }
                }
            }

#if SEARCH_HELPER_ENABLE_CACHING
            UpdateCachedDependencies(searchedCtx.Path, searchedCtx.Dependencies);
#endif
            return searchedCtx;
        }

        public static Asset FindDependencies(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            using var measure = Profiler.Measure($"FindDependencies {obj.name}");

            AssetDatabase.SaveAssets();

            var dependencies = Asset.ToAssets(EditorUtility.CollectDependencies(new[] { obj }), obj);
            var path = AssetDatabase.GetAssetPath(obj);
            var guid = string.Empty;
            var isFolder = false;

            if (!string.IsNullOrEmpty(path))
            {
                guid = AssetDatabase.AssetPathToGUID(path);
                isFolder = AssetDatabase.IsValidFolder(path);
            }

            var objectContext = new Asset()
            {
                Object = obj,
                Path = path,
                Guid = guid,
                IsFolder = isFolder,
                Dependencies = dependencies.ToList()
            };

            return objectContext;
        }

        public static Dictionary<string, List<Asset>> BuildDependencyMap(string root = null, bool useCache = true)
        {
            // Cache is broken with global and local mode
            AssetDatabase.SaveAssets();
            using var measure = Profiler.Measure($"BuildDependencyMap {root ?? "global"}");

#if SEARCH_HELPER_ENABLE_CACHING
            if (useCache && DependencyMap != null && HasFullDependencyMap)
            {
                var DependencyMapCopy = DependencyMap.ToDictionary(k => k.Key, v => v.Value);
                return DependencyMapCopy;
            }
#endif

            var result = new Dictionary<string, List<Asset>>();
            var paths = SearchHelperService.FindAssetPaths(root);
            foreach (var path in paths)
            {
                var dependencies = AssetDatabase.GetDependencies(path);
                foreach (var dependency in dependencies)
                {
                    if (path == dependency)
                    {
                        if(!result.ContainsKey(path))
                        {
                            result.Add(path, new List<Asset>());
                        }
                        EnsureMainKeyExists(path);
                        continue;
                    }

                    var context = Asset.FromPath(path);
#if SEARCH_HELPER_ENABLE_CACHING
                    CacheDependencyObject(context, dependency);
#endif
                    if (result.ContainsKey(dependency))
                    {
                        result[dependency].Add(context);
                    }
                    else
                    {
                        result.Add(dependency, new List<Asset>() { context });
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
                    foreach (var (path, contexts) in result)
                    {
                        UpdateCachedDependencies(path, contexts);
                    }
                }
            }
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

        public static string GetFileHashMd5(ref MD5 md5, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (!File.Exists(path))
            {
                return string.Empty;
            }

            var hashBytes = md5.ComputeHash(File.ReadAllBytes(path));
            var hash = BitConverter.ToString(hashBytes);
            return hash;
        }

        public static string GetFileHashSha256(string path, int skipLines, HashSet<string> ignored = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (!File.Exists(path))
            {
                return string.Empty;
            }

            using var sha = SHA256.Create();
            using var reader = new StreamReader(path, Encoding.UTF8);

            for (var i = 0; i < skipLines && !reader.EndOfStream; i++)
            {
                reader.ReadLine();
            }

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (ignored != null)
                {
                    var skip = false;
                    foreach (var ignore in ignored)
                    {
                        if (line.Contains(ignore))
                        {
                            skip = true;
                            break;
                        }
                    }

                    if (skip)
                        continue;
                }

                var bytes = Encoding.UTF8.GetBytes(line);
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);

                sha.TransformBlock(Encoding.UTF8.GetBytes("\n"), 0, 1, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return Convert.ToBase64String(sha.Hash);
        }

#if SEARCH_HELPER_ENABLE_CACHING
        private static void ClearDependencyMap()
        {
            HasOnlyNames = false;
            HasFullDependencyMap = false;
            DependencyMap = new Dictionary<string, List<Asset>>();
        }

        private static void UpdateCachedDependencies(string main, List<Asset> dependencies = null)
        {
            DependencyMap[main] = dependencies;
        }

        [Conditional("SEARCH_HELPER_ENABLE_CACHING")]
        private static void EnsureMainKeyExists(string main)
        {
            if (!DependencyMap.ContainsKey(main))
            {
                DependencyMap[main] = new List<Asset>();
            }
        }

        [Conditional("SEARCH_HELPER_ENABLE_CACHING")]
        private static void CacheDependencyObject(Asset dependency, string main)
        {
            DependencyMap.TryAdd(main, new List<Asset>());
            if (DependencyMap[main] == null)
            {
                DependencyMap[main] = new List<Asset>() { dependency };
            }
            else
            {
                DependencyMap[main].Add(dependency);
            }
        }
#endif
    }
}