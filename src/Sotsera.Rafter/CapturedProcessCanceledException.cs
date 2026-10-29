namespace Sotsera.Rafter;

/// <summary>Invocation cancellation with immutable bounded output after process/output settlement.</summary>
public sealed class CapturedProcessCanceledException : OperationCanceledException
{
    /// <summary>The configured executable name or path, without presentation redaction.</summary>
    public string Executable { get; }
    /// <summary>The observed root process exit code, or null when no exit code was available.</summary>
    public int? ObservedExitCode { get; }
    /// <summary>Bounded stdout retained through shutdown and output settlement; values remain unredacted.</summary>
    public CapturedStream PartialStandardOutput { get; }
    /// <summary>Bounded stderr retained through shutdown and output settlement; values remain unredacted.</summary>
    public CapturedStream PartialStandardError { get; }
    /// <summary>The configured maximum number of UTF-16 code units retained per stream.</summary>
    public int CaptureLimit { get; }
    /// <summary>The first stream that exceeded its capture limit, or null if neither stream overflowed.</summary>
    public ProcessOutputStream? OverflowedStream { get; }

    internal CapturedProcessCanceledException(string executable, int? observedExitCode,
        CapturedStream standardOutput, CapturedStream standardError, int captureLimit,
        ProcessOutputStream? overflowedStream, CancellationToken cancellationToken)
        : base($"Process cancelled | {executable}", cancellationToken)
    {
        Executable = executable;
        ObservedExitCode = observedExitCode;
        PartialStandardOutput = standardOutput;
        PartialStandardError = standardError;
        CaptureLimit = captureLimit;
        OverflowedStream = overflowedStream;
    }
}
