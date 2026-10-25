namespace Sotsera.Rafter.Internal;

internal static class RootPaths
{
    public static bool HasValidText(string? path) => !string.IsNullOrWhiteSpace(path) && !path.Contains('\0');

    public static string Resolve(string path, string rootDirectory)
    {
        Validate(path, nameof(path));
        return Path.GetFullPath(path, rootDirectory);
    }

    public static string Combine(string rootDirectory, string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Length == 0)
        {
            throw new ArgumentException("At least one path segment is required.", nameof(paths));
        }
        foreach (var path in paths)
        {
            Validate(path, nameof(paths));
        }
        return Path.GetFullPath(Path.Combine(paths), rootDirectory);
    }

    private static void Validate(string? path, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(path, parameterName);
        if (!HasValidText(path))
        {
            throw new ArgumentException("A path must be nonblank and contain no NUL characters.", parameterName);
        }
        if (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("A rooted path must be fully qualified.", parameterName);
        }
    }
}
