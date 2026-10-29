namespace Sotsera.Rafter;

/// <summary>The observed exit of a process whose owned descendants and output have finished.</summary>
public class ProcessResult
{
    /// <summary>Whether the observed exit code belongs to the configured accepted set.</summary>
    public bool Succeeded { get; }
    /// <summary>The root process exit code observed after owned processes and output settled.</summary>
    public int ExitCode { get; }

    internal ProcessResult(int exitCode, bool succeeded)
    {
        ExitCode = exitCode;
        Succeeded = succeeded;
    }
}

/// <summary>How a completed process with a rejected exit code affects its body and invocation.</summary>
public enum ProcessFailureAction
{
    /// <summary>Return the rejected exit result without failing the invocation.</summary>
    Continue,
    /// <summary>Return the rejected exit result and mark the invocation failed.</summary>
    Fail,
    /// <summary>Throw ProcessFailureException for the rejected exit result.</summary>
    Throw
}
