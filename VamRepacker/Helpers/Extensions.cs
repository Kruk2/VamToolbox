using System;
using System.Collections.Generic;
using System.IO;

namespace VamRepacker.Helpers
{
    public static class Extensions
    {
        public static IEnumerable<T> Tap<T>(this IEnumerable<T> enumerable, Action<T> fn)
            where T : notnull
        {
            foreach (var value in enumerable)
            {
                fn(value);
                yield return value;
            }
        }

        public static string RelativeTo(this string path, string root)
        {
            if (!path.StartsWith(root, StringComparison.InvariantCultureIgnoreCase)) throw new InvalidOperationException($"Path '{path}' does not start with '{root}'");
            return path.Substring(root.Length + 1);
        }

        public static string NormalizePathSeparators(this string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        public static string RemoveInvalidChars(this string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
