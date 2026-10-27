namespace Sotsera.Rafter;

/// <summary>Configures a dotnet publish invocation.</summary>
public sealed class DotnetPublishCommand : DotnetCommand<DotnetPublishCommand>
{
    internal DotnetPublishCommand(ProcessCommand command) : base(command) { }

    /// <summary>Skips building; the .NET CLI also disables implicit restore.</summary>
    public DotnetPublishCommand NoBuild() => Arguments("--no-build");
}
