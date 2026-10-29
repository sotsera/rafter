namespace Sotsera.Rafter;

/// <summary>A settled capture timeout with bounded output, including any retained shutdown diagnostics.</summary>
public sealed class CapturedProcessTimeoutException : ProcessTimeoutException
{
    /// <summary>Bounded stdout retained through shutdown and output settlement; values remain unredacted.</summary>
    public CapturedStream PartialStandardOutput { get; }
    /// <summary>Bounded stderr retained through shutdown and output settlement; values remain unredacted.</summary>
    public CapturedStream PartialStandardError { get; }
    /// <summary>The configured maximum number of UTF-16 code units retained per stream.</summary>
    public int CaptureLimit { get; }
    /// <summary>The first stream that exceeded its capture limit, or null if neither stream overflowed.</summary>
    public ProcessOutputStream? OverflowedStream { get; }

    internal CapturedProcessTimeoutException(string executable, TimeSpan timeout, TimeSpan elapsed,
        int? observedExitCode, CapturedStream standardOutput, CapturedStream standardError,
        int captureLimit, ProcessOutputStream? overflowedStream) : base(executable, timeout, elapsed, observedExitCode)
    {
        PartialStandardOutput = standardOutput;
        PartialStandardError = standardError;
        CaptureLimit = captureLimit;
        OverflowedStream = overflowedStream;
    }
}
