using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor
{
    public class ObjectContext
    {
        private Object _object;
        public Object Object 
        {
            get
            {
                if (_object == null && !string.IsNullOrEmpty(Path))
                {
                    _object = AssetDatabase.LoadMainAssetAtPath(Path);
                }

                return _object;
            }
            set => _object = value;
        }

        private string _path;

        public string Path
        {
            get
            {
                if (string.IsNullOrEmpty(_path) && !string.IsNullOrEmpty(Guid))
                {
                    _path = AssetDatabase.GUIDToAssetPath(Guid);
                }

                return _path;
            }
            set => _path = value;
        }

        private string _guid;

        public string Guid
        {
            get
            {
                if (string.IsNullOrEmpty(_guid) && !string.IsNullOrEmpty(Path))
                {
                    _guid = AssetDatabase.AssetPathToGUID(Path);
                }

                return _guid;
            }
            set => _guid = value;
        }

        public bool IsFolder { get; set; }

        public List<ObjectContext> Dependencies;

        public bool IsExpanded { get; set; } = true;

        //public bool IsValid => Object != null && !string.IsNullOrEmpty(Path);

        private const string NoGuidText = "NO GUID";

        public static ObjectContext ToObjectContext(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(obj);
            var guid = NoGuidText;

            return new ObjectContext()
            {
                Object = obj,
                Path = path,
                Dependencies = new List<ObjectContext>(),
            };
        }

        public static IEnumerable<ObjectContext> ToObjectContexts(IEnumerable<Object> objects, Object mainObject = null)
        {
            if (objects == null)
            {
                return null;
            }

            if (mainObject != null)
            {
                objects = objects.Where(value => value != mainObject).ToArray();
            }

            return objects.Select(ToObjectContext);
        }
        
        public static ObjectContext FromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return new ObjectContext()
            {
                Path = path,
                Dependencies = new List<ObjectContext>(),
            };
        }
    }
}