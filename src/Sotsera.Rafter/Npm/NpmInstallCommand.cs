namespace Sotsera.Rafter;

/// <summary>Configures npm install for the current package manifest.</summary>
public sealed class NpmInstallCommand : NpmDependencyCommand<NpmInstallCommand>
{
    internal NpmInstallCommand(ProcessCommand command) : base(command) { }
}
