namespace Sotsera.Rafter;

/// <summary>A completed process returned an exit code outside its accepted set.</summary>
public sealed class ProcessFailureException : Exception
{
    /// <summary>The settled process result; a capture operation provides a CapturedProcessResult.</summary>
    public ProcessResult Result { get; }
    /// <summary>The configured executable name or path, without presentation redaction.</summary>
    public string Executable { get; }
    /// <summary>The immutable set of exit codes accepted by this operation.</summary>
    public IReadOnlyList<int> AcceptedExitCodes { get; }

    internal ProcessFailureException(string executable, int[] accepted, ProcessResult result)
        : base($"Process '{executable}' exited with code {result.ExitCode}; accepted: {string.Join(", ", accepted)}.")
    {
        Executable = executable;
        Result = result;
        AcceptedExitCodes = Array.AsReadOnly(accepted);
    }
}
