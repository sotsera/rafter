namespace Sotsera.Rafter;

/// <summary>Capture exceeded its bound and the owned process/output scope subsequently settled.</summary>
public sealed class ProcessCaptureLimitException : Exception
{
    public string Executable { get; }
    public ProcessOutputStream Stream { get; }
    public int Limit { get; }
    public CapturedStream PartialStandardOutput { get; }
    public CapturedStream PartialStandardError { get; }
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
