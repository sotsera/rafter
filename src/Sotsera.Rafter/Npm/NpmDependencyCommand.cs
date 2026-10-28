namespace Sotsera.Rafter;

/// <summary>Options shared by npm install and ci commands.</summary>
/// <typeparam name="TCommand">The concrete dependency command.</typeparam>
public abstract class NpmDependencyCommand<TCommand> : ToolCommand<TCommand>
    where TCommand : NpmDependencyCommand<TCommand>
{
    private protected NpmDependencyCommand(ProcessCommand command) : base(command) { }

    /// <summary>Disables npm's package audit for this command.</summary>
    public TCommand NoAudit() => Arguments("--no-audit");

    /// <summary>Disables npm funding messages for this command.</summary>
    public TCommand NoFund() => Arguments("--no-fund");

    /// <summary>Disables package lifecycle scripts during dependency installation.</summary>
    public TCommand IgnoreScripts() => Arguments("--ignore-scripts");
}
