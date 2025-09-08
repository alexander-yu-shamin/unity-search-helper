using System.Collections.Generic;
using UnityEngine;

namespace SearchHelper.Editor
{
    public class ObjectContext
    {
        public Object Object { get; set; }
        public string Path { get; set; }
        public string Guid { get; set; }

        public List<ObjectContext> Dependencies;

        public bool IsValid => Object != null && !string.IsNullOrEmpty(Path);
    }
}