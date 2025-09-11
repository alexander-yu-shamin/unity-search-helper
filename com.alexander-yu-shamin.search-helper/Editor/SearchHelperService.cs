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

        public static IEnumerable<Object> FindAssets(string root = null)
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

            var dependencies = ToObjectContexts(EditorUtility.CollectDependencies(new[] { obj }), obj);
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

        public static ObjectContext ToObjectContext(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var objectContext = new ObjectContext()
            {
                Object = obj,
                Path = AssetDatabase.GetAssetPath(obj)
            };

            if (!string.IsNullOrEmpty(objectContext.Path))
            { 
                objectContext.Guid = AssetDatabase.AssetPathToGUID(objectContext.Path, AssetPathToGUIDOptions.OnlyExistingAssets);
            }

            return objectContext;
        }

        public static IEnumerable<ObjectContext> ToObjectContexts(Object[] objects, Object mainObject = null)
        {
            if (objects == null)
            {
                return null;
            }

            if (mainObject != null)
            {
                objects = objects.Where(value => value != mainObject).ToArray();
            }

            return objects.Select(element =>
            {
                var path = AssetDatabase.GetAssetPath(element);
                var guid = !string.IsNullOrEmpty(path)
                    ? AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets) 
                    : "no guid";

                return new ObjectContext()
                {
                    Object = element,
                    Path = path,
                    Guid = guid
                };
            });
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