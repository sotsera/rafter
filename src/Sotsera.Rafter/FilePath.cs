using System.Text;

namespace Sotsera.Rafter;

/// <summary>An immutable absolute file path. Constructing a handle performs no filesystem I/O.</summary>
public sealed class FilePath
{
    private static readonly Encoding _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The absolute path, suitable for .NET APIs and tool arguments.</summary>
    public string FullPath { get; }

    internal FilePath(string fullPath)
    {
        FullPath = fullPath;
    }

    /// <summary>Probes this file using System.IO.File.Exists semantics.</summary>
    public bool Exists() => System.IO.File.Exists(FullPath);

    /// <summary>Creates or overwrites this file with UTF-8 text without a BOM or added newline.
    /// Parents must exist. The caller must await completion, which includes closing the file, and choose cancellation explicitly.</summary>
    public Task WriteAllTextAsync(string contents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contents);
        return System.IO.File.WriteAllTextAsync(FullPath, contents, _utf8, cancellationToken);
    }
}
