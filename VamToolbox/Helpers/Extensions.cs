namespace VamToolbox.Helpers;

public static class Extensions
{
    public static string RelativeTo(this string path, string root)
    {
        if (!path.StartsWith(root, StringComparison.InvariantCultureIgnoreCase)) throw new InvalidOperationException($"Path '{path}' does not start with '{root}'");
        return path[(root.Length + 1)..];
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
}