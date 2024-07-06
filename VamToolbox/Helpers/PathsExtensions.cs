using System.IO.Abstractions;
using VamToolbox.Models;

namespace VamToolbox.Helpers;

public static class PathsExtensions
{
    public static string RelativeTo(this string path, string root)
    {
        if (!path.StartsWith(root, StringComparison.InvariantCultureIgnoreCase)) throw new InvalidOperationException($"Path '{path}' does not start with '{root}'");
        return path[(root.Length + 1)..];
    }

    public static string SimplifyRelativePath(this IFileSystem fs, string localFolder, string assetPath)
    {
        var relativePath = fs.Path.Combine(localFolder, assetPath);
        return fs.Path.GetFullPath(relativePath, "Z:/").NormalizePathSeparators().Replace("Z:/", "", StringComparison.Ordinal);
    }

    public static string NormalizePathSeparators(this string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }

    public static string NormalizeAssetPath(this string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    public static string RemoveInvalidChars(this string filename)
    {
        return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
    }

    public static IEnumerable<T> SelfAndChildren<T>(this IEnumerable<T> files) where T: FileReferenceBase
    {
        return files.SelectMany(t => t.SelfAndChildren()).Distinct().Cast<T>();
    }
}