namespace Sotsera.Rafter;

/// <summary>Capture exceeded its bound and the owned process/output scope subsequently settled.</summary>
public sealed class ProcessCaptureLimitException : Exception
{
    /// <summary>The configured executable name or path, without presentation redaction.</summary>
    public string Executable { get; }
    /// <summary>The first output stream that exceeded the configured limit.</summary>
    public ProcessOutputStream Stream { get; }
    /// <summary>The maximum number of UTF-16 code units retained per stream.</summary>
    public int Limit { get; }
    /// <summary>Bounded stdout retained through shutdown and output settlement; values remain unredacted.</summary>
    public CapturedStream PartialStandardOutput { get; }
    /// <summary>Bounded stderr retained through shutdown and output settlement; values remain unredacted.</summary>
    public CapturedStream PartialStandardError { get; }
    /// <summary>The observed root process exit code, or null when no exit code was available.</summary>
    public int? ObservedExitCode { get; }

    internal ProcessCaptureLimitException(string executable, ProcessOutputStream stream, int limit,
        CapturedStream standardOutput, CapturedStream standardError, int? observedExitCode)
        : base($"Process capture limit exceeded | {executable} | "
            + $"{(stream == ProcessOutputStream.StandardOutput ? "stdout" : "stderr")} | limit: {limit} UTF-16 code units")
    {
        Executable = executable;
        Stream = stream;
        Limit = limit;
        PartialStandardOutput = standardOutput;
        PartialStandardError = standardError;
        ObservedExitCode = observedExitCode;
    }
}
