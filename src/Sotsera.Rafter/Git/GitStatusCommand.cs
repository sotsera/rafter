namespace Sotsera.Rafter;

/// <summary>Configures git status without parsing or changing its result.</summary>
public sealed class GitStatusCommand : ToolCommand<GitStatusCommand>
{
    internal GitStatusCommand(ProcessCommand command) : base(command) { }

    /// <summary>Selects short output.</summary>
    public GitStatusCommand Short() => Arguments("--short");

    /// <summary>Includes branch information.</summary>
    public GitStatusCommand Branch() => Arguments("--branch");

    /// <summary>Selects stable version-one porcelain output.</summary>
    public GitStatusCommand Porcelain() => Arguments("--porcelain=v1");
}
