namespace Sotsera.Rafter;

/// <summary>Configures a lockfile-based npm ci invocation.</summary>
public sealed class NpmCiCommand : NpmDependencyCommand<NpmCiCommand>
{
    internal NpmCiCommand(ProcessCommand command) : base(command) { }
}
