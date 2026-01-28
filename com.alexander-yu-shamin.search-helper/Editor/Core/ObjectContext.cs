using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor
{
    public enum ObjectState
    {
        None,
        BaseObject,
        SameAsBaseObject,
        NotTheSameAsBaseObject
    }
    
    public enum ObjectContextTarget
    {
        NoTarget,
        Path,
        Name,
        Type,
    }

    public class ObjectContext
    {
        private Object _object;
        public Object Object 
        {
            get
            {
                if (_object == null && !string.IsNullOrEmpty(_path))
                {
                    _object = AssetDatabase.LoadMainAssetAtPath(_path);
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
                if (string.IsNullOrEmpty(_path) && !string.IsNullOrEmpty(_guid))
                {
                    _path = AssetDatabase.GUIDToAssetPath(_guid);
                }

                return _path;
            }
            set => _path = value;
        }

        public string MetaPath => Path + ".meta";

        private string _guid;

        public string Guid
        {
            get
            {
                if (string.IsNullOrEmpty(_guid) && !string.IsNullOrEmpty(_path))
                {
                    _guid = AssetDatabase.AssetPathToGUID(_path);
                }

                return _guid;
            }
            set => _guid = value;
        }

        private string _size = null;
        public string Size
        {
            get
            {
                if (_size != null)
                {
                    return _size;
                }

                if (!string.IsNullOrEmpty(Path))
                {
                    if (File.Exists(Path))
                    {
                        _size = FormatFileSize(new FileInfo(Path).Length);
                    }
                    else
                    {
                        _size = "NaN";
                    }

                    return _size;
                }

                return null;
            }
        }

        public bool IsFolder { get; set; }
        public bool IsSelected { get; set; } = true;
        public bool IsBaseObject { get; set; } = false;
        public bool IsMerged { get; set; } = false;
        public ObjectState State { get; set; } = ObjectState.None;

        public List<ObjectContext> Dependencies;

        public bool IsExpanded { get; set; } = true;

        public bool ShouldBeShown { get; set; } = true;

        public ObjectContext()
        {
        }

        public ObjectContext(ObjectContext context)
        {
            _guid = context._guid;
            _object = context._object;
            _path = context._path;
        }

        public static ObjectContext ToObjectContext(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(obj);

            return new ObjectContext()
            {
                Object = obj,
                Path = path,
                IsFolder = AssetDatabase.IsValidFolder(path),
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
                IsFolder = AssetDatabase.IsValidFolder(path),
                Dependencies = new List<ObjectContext>(),
            };
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes == 0)
            {
                return "0 B";
            }

            if (bytes == 1)
            {
                return "1 B";
            }

            var absBytes = Math.Abs(bytes);

            if (absBytes < 1024)
            {
                return $"{bytes} B";
            }

            if (absBytes < 1024 * 1024)
            {
                return $"{(bytes / 1024.0):0.##} KB";
            }

            if (absBytes < 1024L * 1024 * 1024)
            {
                return $"{(bytes / (1024.0 * 1024)):0.##} MB";
            }

            if (absBytes < 1024L * 1024 * 1024 * 1024)
            {
                return $"{(bytes / (1024.0 * 1024 * 1024)):0.##} GB";
            }

            return $"{(bytes / (1024.0 * 1024 * 1024 * 1024)):0.##} TB";
        }

        public string GetTarget(ObjectContextTarget target)
        {
            return target switch
            {
                ObjectContextTarget.Path => Path,
                ObjectContextTarget.Name => Object != null ? Object.name : string.Empty,
                ObjectContextTarget.Type => Object != null ? Object.GetType().Name : string.Empty,
                ObjectContextTarget.NoTarget => string.Empty,
                _                        => throw new ArgumentOutOfRangeException(nameof(target), target, null)
            };
        }
    }
}