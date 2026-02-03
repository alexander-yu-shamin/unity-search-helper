using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SearchHelper.Editor
{
    public enum AssetDiffState
    {
        None,
        BaseObject,
        SameAsBaseObject,
        NotTheSameAsBaseObject,
    }
    
    public enum AssetTarget
    {
        NoTarget,
        Path,
        Name,
        Type,
    }

    [Flags]
    public enum AssetState
    {
        None = 0,
        FilterByRule = 1 << 1,
        FilterByString = 1 << 2,
        HideFolders = 1 << 3,
        HideDependencies = 1 << 4,
        HideEmptyDependencies = 1 << 5,
        Foldout = 1 << 6
    }

    public class Asset
    {
        #region Merge
        public AssetDiffState MetaDiffState { get; set; } = AssetDiffState.None;
        public AssetDiffState DiffState { get; set; } = AssetDiffState.None;

        public bool IsSelected { get; set; } = true;
        public bool IsBaseObject { get; set; } = false;
        public bool IsMerged { get; set; } = false;
        #endregion

        public bool IsFolder { get; set; }
        public List<Asset> Dependencies;
        public AssetState State { get; set; } = AssetState.Foldout;

        public bool IsFoldout
        {
            get => (State & AssetState.Foldout) != 0;
            set => State = value ? State | AssetState.Foldout : State & ~AssetState.Foldout;
        }

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

        private (long bytes, string readable)? _sizeInfo = null;

        private (long Bytes, string Readable) SizeInfo =>
            _sizeInfo ??= CalculateSize();

        public string ReadableSize => SizeInfo.Readable;
        public long Size => SizeInfo.Bytes;

        private (long Bytes, string Readable) CalculateSize()
        {
            if (string.IsNullOrEmpty(Path) || !File.Exists(Path))
                return (0, "NaN");

            try
            {
                var length = new FileInfo(Path).Length;
                return (length, FormatExtensions.ToHumanReadableSize(length));
            }
            catch
            {
                return (0, "NaN");
            }
        }

        public Asset()
        {
        }

        public Asset(Asset context)
        {
            _guid = context._guid;
            _object = context._object;
            _path = context._path;
        }

        public static Asset ToAsset(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(obj);

            return new Asset()
            {
                Object = obj,
                Path = path,
                IsFolder = AssetDatabase.IsValidFolder(path),
                Dependencies = new List<Asset>(),
            };
        }

        public static IEnumerable<Asset> ToAssets(IEnumerable<Object> objects, Object mainObject = null)
        {
            if (objects.IsNullOrEmpty())
            {
                return null;
            }

            if (mainObject != null)
            {
                objects = objects.Where(value => value != null && value != mainObject).ToArray();
            }

            return objects.Select(ToAsset);
        }
        
        public static Asset FromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return new Asset()
            {
                Path = path,
                IsFolder = AssetDatabase.IsValidFolder(path),
                Dependencies = new List<Asset>(),
            };
        }

        public string GetTarget(AssetTarget target)
        {
            return target switch
            {
                AssetTarget.Path => Path,
                AssetTarget.Name => Object != null ? Object.name : string.Empty,
                AssetTarget.Type => Object != null ? Object.GetType().Name : string.Empty,
                AssetTarget.NoTarget => string.Empty,
                _                        => throw new ArgumentOutOfRangeException(nameof(target), target, null)
            };
        }
    }
}