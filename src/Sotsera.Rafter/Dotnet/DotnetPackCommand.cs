namespace Sotsera.Rafter;

/// <summary>Configures a dotnet pack invocation.</summary>
public sealed class DotnetPackCommand : DotnetCommand<DotnetPackCommand>
{
    internal DotnetPackCommand(ProcessCommand command) : base(command) { }

    /// <summary>Skips building; the .NET CLI also disables implicit restore.</summary>
    public DotnetPackCommand NoBuild() => Arguments("--no-build");
}
