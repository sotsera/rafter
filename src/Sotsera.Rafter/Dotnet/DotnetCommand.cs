using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>Shared fluent configuration and execution for typed .NET CLI commands.</summary>
/// <typeparam name="TCommand">The concrete command type retained by fluent calls.</typeparam>
public abstract class DotnetCommand<TCommand> : ToolCommand<TCommand> where TCommand : DotnetCommand<TCommand>
{
    private protected DotnetCommand(ProcessCommand command) : base(command) { }

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

}
