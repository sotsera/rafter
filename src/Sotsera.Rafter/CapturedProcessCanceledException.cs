namespace Sotsera.Rafter;

/// <summary>Invocation cancellation with immutable bounded output after process/output settlement.</summary>
public sealed class CapturedProcessCanceledException : OperationCanceledException
{
    public string Executable { get; }
    public int? ObservedExitCode { get; }
    public CapturedStream PartialStandardOutput { get; }
    public CapturedStream PartialStandardError { get; }
    public int CaptureLimit { get; }
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
