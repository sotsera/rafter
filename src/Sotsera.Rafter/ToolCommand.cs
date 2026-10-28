namespace Sotsera.Rafter;

/// <summary>Composes fluent tool-specific commands over the ordinary owned process pipeline.</summary>
/// <typeparam name="TCommand">The concrete command type retained by fluent configuration.</typeparam>
public abstract class ToolCommand<TCommand> where TCommand : ToolCommand<TCommand>
{
    private readonly ProcessCommand _command;

    /// <summary>Wraps a fresh process command without starting it.</summary>
    protected ToolCommand(ProcessCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _command = command;
    }

    /// <summary>Appends separate raw argument tokens in call order.</summary>
    public TCommand Arguments(params string[] arguments)
    {
        _command.Arguments(arguments);
        return (TCommand)this;
    }

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

    /// <summary>Starts the child and awaits owned process, stream, and callback settlement.</summary>
    public Task<ProcessResult> RunAsync() => _command.RunAsync();

    /// <summary>Runs with bounded decoded capture through the same owned process pipeline.</summary>
    public Task<CapturedProcessResult> RunAndCaptureAsync(int maxCharactersPerStream, bool echo = false,
        Func<CapturedProcessResult, ProcessFailureAction>? onFailure = null)
        => _command.RunAndCaptureAsync(maxCharactersPerStream, echo, onFailure);
}
