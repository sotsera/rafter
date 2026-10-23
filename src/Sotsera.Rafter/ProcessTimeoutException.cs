namespace Sotsera.Rafter;

/// <summary>A process execution deadline expired and its owned processes and output have settled.</summary>
public class ProcessTimeoutException : TimeoutException
{
    public string Executable { get; }
    public TimeSpan Timeout { get; }
    public TimeSpan Elapsed { get; }
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
