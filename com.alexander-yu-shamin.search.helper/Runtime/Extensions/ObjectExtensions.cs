using System;

namespace Search.Helper.Runtime.Extensions
{
    public static class ObjectExtensions 
    {
        public static void Safe<T>(this T obj, Action<T> action) where T : class
        {
            if (obj != null)
            {
                action?.Invoke(obj);
            }
        }
    }
}
