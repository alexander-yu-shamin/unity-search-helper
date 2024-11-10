using System.Text;

namespace Search.Helper.Runtime.Extensions
{
    public static class StringExtensions
    {
        public static string AddSpacesBeforeUppercase(this string text)
        {
            var builder = new StringBuilder();

            foreach (var c in text)
            {
                if (char.IsUpper(c) && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
