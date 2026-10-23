namespace Sotsera.Rafter;

/// <summary>A settled capture timeout with bounded output, including any retained shutdown diagnostics.</summary>
public sealed class CapturedProcessTimeoutException : ProcessTimeoutException
{
    public CapturedStream PartialStandardOutput { get; }
    public CapturedStream PartialStandardError { get; }
    public int CaptureLimit { get; }
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
