using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace SearchHelper.Editor.Core.Sort
{
    public class DiffManager : ObservableData
    {
        // Diff
        public readonly HashSet<string> DefaultLines = new() { "assetBundleName", "assetBundleVariant", "SpriteID", "userData" };
        public HashSet<string> IgnoredLines { get; set; } = new HashSet<string>();
        public HashSet<string> PossibleIgnoredLines => IgnoredLines.Union(DefaultLines).ToHashSet();

        private MD5 _md5Hasher = MD5.Create();
        private const int DefaultIgnoredLines = 2;

        public string GetFileHashMd5(string filePath)
        {
            return SearchHelperService.GetFileHashMd5(ref _md5Hasher, filePath);
        }

        public bool? CompareMetaFiles(string basePath, string theirsPath)
        {
            var baseHash = GetFileHashSha256(basePath, 2);
            var theirsHash = GetFileHashSha256(theirsPath, 2);

            if (string.IsNullOrEmpty(baseHash) || string.IsNullOrEmpty(theirsHash))
            {
                return null;
            }

            return baseHash == theirsHash;
        }

        public bool? CompareFilesBinary(string basePath, string theirsPath)
        {
            var baseHash = GetFileHashMd5(basePath);
            var theirsHash = GetFileHashMd5(theirsPath);

            if (string.IsNullOrEmpty(baseHash) || string.IsNullOrEmpty(theirsHash))
            {
                return null;
            }

            return baseHash == theirsHash;
        }

        public string GetFileHashSha256(string path, int skipLines = DefaultIgnoredLines)
        {
            return SearchHelperService.GetFileHashSha256(path, skipLines, IgnoredLines);
        }

        public void AddToIgnoreLines(string line)
        {
            IgnoredLines.Add(line);
            OnDataChanged();
        }

        public void RemoveLine(string line)
        {
            IgnoredLines.Remove(line);
            OnDataChanged();
        }

        public void UpdateState()
        {
            OnDataChanged();
        }
    }
}
