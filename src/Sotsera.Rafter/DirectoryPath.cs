using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>An immutable absolute directory path. Constructing a handle performs no filesystem I/O.</summary>
public sealed class DirectoryPath
{
    /// <summary>The normalized absolute path, suitable for .NET APIs and tool arguments.</summary>
    public string FullPath { get; }

    internal DirectoryPath(string fullPath)
    {
        FullPath = Path.TrimEndingDirectorySeparator(fullPath);
    }

    /// <summary>Combines segments and resolves a directory path against this directory without performing I/O.</summary>
    public DirectoryPath Directory(params string[] paths) => new(RootPaths.Combine(FullPath, paths));

    /// <summary>Combines segments and resolves a file path against this directory without performing I/O.</summary>
    public FilePath File(params string[] paths) => new(RootPaths.Combine(FullPath, paths));

    /// <summary>Probes this directory using System.IO.Directory.Exists semantics.</summary>
    public bool Exists() => System.IO.Directory.Exists(FullPath);

    /// <summary>Creates this directory and missing parents, preserving an existing directory, and returns this handle.</summary>
    public DirectoryPath EnsureExists()
    {
        System.IO.Directory.CreateDirectory(FullPath);
        return this;
    }
}
