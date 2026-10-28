namespace Sotsera.Rafter;

/// <summary>Immutable reusable Git defaults. Selecting a command creates an independent invocation.</summary>
public sealed class GitExecutable
{
    private readonly Executable _executable;

    internal GitExecutable(Executable executable) => _executable = executable;

    /// <summary>Returns defaults with a child-only environment value.</summary>
    public GitExecutable Environment(string name, string value) => new(_executable.Environment(name, value));

    /// <summary>Returns defaults removing child environment variables.</summary>
    public GitExecutable RemoveEnvironment(params string[] names) => new(_executable.RemoveEnvironment(names));

    /// <summary>Returns defaults selecting the repository's working directory.</summary>
    public GitExecutable WorkingDirectory(string path) => new(_executable.WorkingDirectory(path));

    /// <summary>Returns defaults with a positive per-process execution deadline.</summary>
    public GitExecutable Timeout(TimeSpan timeout) => new(_executable.Timeout(timeout));

    /// <summary>Creates a fresh untyped Git command with raw argument tokens.</summary>
    public ProcessCommand Arguments(params string[] arguments) => _executable.Arguments(arguments);

    /// <summary>Creates a fresh working-tree status command.</summary>
    public GitStatusCommand Status() => new(_executable.Arguments("status"));

    /// <summary>Creates a fresh commit-history command.</summary>
    public GitLogCommand Log() => new(_executable.Arguments("log"));
}
