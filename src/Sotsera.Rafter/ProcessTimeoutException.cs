namespace Sotsera.Rafter;

/// <summary>A process execution deadline expired and its owned processes and output have settled.</summary>
public class ProcessTimeoutException : TimeoutException
{
    /// <summary>The configured executable name or path, without presentation redaction.</summary>
    public string Executable { get; }
    /// <summary>The configured execution deadline, which initiates shutdown when it expires.</summary>
    public TimeSpan Timeout { get; }
    /// <summary>The measured elapsed execution time recorded for this timeout.</summary>
    public TimeSpan Elapsed { get; }
    /// <summary>The observed root process exit code, or null when no exit code was available.</summary>
    public int? ObservedExitCode { get; }

    internal ProcessTimeoutException(string executable, TimeSpan timeout, TimeSpan elapsed, int? observedExitCode)
        : base($"Process timed out | {executable}")
    {
        Executable = executable;
        Timeout = timeout;
        Elapsed = elapsed;
        ObservedExitCode = observedExitCode;
    }
}
