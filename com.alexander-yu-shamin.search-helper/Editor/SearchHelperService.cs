using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Android.Gradle;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor
{
    public class SearchHelperService : AssetPostprocessor
    {
        private const string ObjectSearchFilter = "t:Object";

        public static IEnumerable<ObjectContext> FindAllAssets(string root = null)
        {
            var guids = AssetDatabase.FindAssets(ObjectSearchFilter, GetSearchDirs(root));

            var objects = guids.Select(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadMainAssetAtPath(path);

                if (obj == null)
                {
                    return null;
                }

                return new ObjectContext()
                {
                    Guid = guid,
                    Path = path,
                    Object = obj,
                    Dependencies = new List<ObjectContext>()
                };
            }).Where(ctx => ctx != null);

            return objects;
        }

        public static IEnumerable<string> FindAssetPaths(string root = null)
        {
            return AssetDatabase.FindAssets(ObjectSearchFilter, GetSearchDirs(root))
                                .Select(AssetDatabase.GUIDToAssetPath)
                                .Where(path => !string.IsNullOrEmpty(path));
        }

        public static IEnumerable<Object> FindAssetObjects(string root = null)
        {
            return AssetDatabase.FindAssets(ObjectSearchFilter, GetSearchDirs(root))
                                .Select(AssetDatabase.GUIDToAssetPath)
                                .Select(AssetDatabase.LoadMainAssetAtPath)
                                .Where(asset => asset != null);
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

        public static ObjectContext FindDependencies(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

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

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (!didDomainReload)
            {
            }
        }
    }
}