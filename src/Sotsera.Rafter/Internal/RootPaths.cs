namespace Sotsera.Rafter.Internal;

internal static class RootPaths
{
    public static bool HasValidText(string? path) => !string.IsNullOrWhiteSpace(path) && !path.Contains('\0');

    public static string Resolve(string path, string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!HasValidText(path))
        {
            throw new ArgumentException("A path must be nonblank and contain no NUL characters.", nameof(path));
        }
        if (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("A rooted path must be fully qualified.", nameof(path));
        }
        return Path.GetFullPath(path, rootDirectory);
    }
}
