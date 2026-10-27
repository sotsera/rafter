using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>Shared fluent configuration and execution for typed .NET CLI commands.</summary>
/// <typeparam name="TCommand">The concrete command type retained by fluent calls.</typeparam>
public abstract class DotnetCommand<TCommand> where TCommand : DotnetCommand<TCommand>
{
    private readonly ProcessCommand _command;

    private protected DotnetCommand(ProcessCommand command) => _command = command;

    /// <summary>Appends raw argument tokens in call order without shell interpretation.</summary>
    public TCommand Arguments(params string[] arguments)
    {
        _command.Arguments(arguments);
        return (TCommand)this;
    }

    /// <summary>Selects a build configuration, including custom configuration names.</summary>
    public TCommand Configuration(string configuration)
    {
        ProcessConfiguration.CheckText(configuration, nameof(configuration));
        return Arguments("--configuration", configuration);
    }

    /// <summary>Selects the CLI output directory. Relative paths retain the CLI's working-directory semantics.</summary>
    public TCommand Output(string path)
    {
        ProcessConfiguration.CheckText(path, nameof(path));
        return Arguments("--output", path);
    }

    /// <summary>Disables implicit package restore.</summary>
    public TCommand NoRestore() => Arguments("--no-restore");

    /// <summary>Disables persistent build servers for this invocation.</summary>
    public TCommand DisableBuildServers() => Arguments("--disable-build-servers");

    /// <summary>Overrides the executable's timeout default once for this child.</summary>
    public TCommand Timeout(TimeSpan timeout)
    {
        _command.Timeout(timeout);
        return (TCommand)this;
    }

    /// <summary>Overrides the executable's working-directory default once for this child.</summary>
    public TCommand WorkingDirectory(string path)
    {
        _command.WorkingDirectory(path);
        return (TCommand)this;
    }

    /// <summary>Overrides one environment name for this child.</summary>
    public TCommand Environment(string name, string value)
    {
        _command.Environment(name, value);
        return (TCommand)this;
    }

    /// <summary>Removes variables from this child's environment.</summary>
    public TCommand RemoveEnvironment(params string[] names)
    {
        _command.RemoveEnvironment(names);
        return (TCommand)this;
    }

    /// <summary>Selects accepted exit codes once for this child.</summary>
    public TCommand AcceptExitCodes(params int[] exitCodes)
    {
        _command.AcceptExitCodes(exitCodes);
        return (TCommand)this;
    }

    /// <summary>Handles this child's completed rejected exit.</summary>
    public TCommand OnFailure(Func<ProcessResult, ProcessFailureAction> handler)
    {
        _command.OnFailure(handler);
        return (TCommand)this;
    }

    /// <summary>Starts the child and awaits owned process, output, and callback settlement.</summary>
    public Task<ProcessResult> RunAsync() => _command.RunAsync();

    /// <summary>Runs with bounded decoded capture through the ordinary owned process pipeline.</summary>
    public Task<CapturedProcessResult> RunAndCaptureAsync(int maxCharactersPerStream, bool echo = false,
        Func<CapturedProcessResult, ProcessFailureAction>? onFailure = null)
        => _command.RunAndCaptureAsync(maxCharactersPerStream, echo, onFailure);
}
