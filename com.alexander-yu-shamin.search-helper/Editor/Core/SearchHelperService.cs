using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor.Core
{
    public class SearchHelperService : AssetPostprocessor
    {
        private const string ObjectSearchFilter = "t:Object";

        public static IEnumerable<string> FindAssetPaths(string root = null)
        {
            AssetDatabase.SaveAssets();

            return FindAssets(ObjectSearchFilter, root).Select(AssetDatabase.GUIDToAssetPath)
                                                       .Where(path => !string.IsNullOrEmpty(path));
        }

        public static IEnumerable<Object> FindAssetObjects(string root = null)
        {
            AssetDatabase.SaveAssets();

            return FindAssets(ObjectSearchFilter, root).Select(AssetDatabase.GUIDToAssetPath)
                                                       .Select(AssetDatabase.LoadMainAssetAtPath)
                                                       .Where(asset => asset != null);
        }

        public static IEnumerable<string> FindAssets(string searchFilter, string root = null)
        {
            return AssetDatabase.FindAssets(searchFilter, GetSearchDirs(root));
        }

        public static ObjectContext FindUsedBy(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            AssetDatabase.SaveAssets();

            var searchedCtx = ObjectContext.ToObjectContext(obj);

            var paths = SearchHelperService.FindAssetPaths();
            if (!paths.Any())
            {
                return null;
            }

            foreach (var path in paths)
            {
                if (path == searchedCtx.Path)
                {
                    continue;
                }

                var dependencies = AssetDatabase.GetDependencies(path);
                foreach (var dependency in dependencies)
                {
                    if (dependency == searchedCtx.Path)
                    {
                        searchedCtx.Dependencies.Add(ObjectContext.FromPath(path));
                        break;
                    }
                }
            }

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
            if (Application.isPlaying)
            {
                return;
            }
        }
    }
}