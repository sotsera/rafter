namespace Sotsera.Rafter;

/// <summary>Immutable reusable .NET CLI defaults. Command selection creates an independent invocation.</summary>
public sealed class DotnetExecutable
{
    private readonly Executable _executable;

    internal DotnetExecutable(Executable executable) => _executable = executable;

    /// <summary>Removes inherited MSBuild/SDK selection overrides so dotnet can select the SDK for the child directory.
    /// Does not pin an SDK, remove DOTNET_HOST_PATH, or change the parent environment.</summary>
    public DotnetExecutable UseWorkingDirectorySdk()
        => new(_executable.RemoveEnvironment("MSBuildSDKsPath", "MSBUILD_EXE_PATH", "MSBuildExtensionsPath",
            "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR", "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER",
            "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"));

    /// <summary>Returns defaults with a child-only environment value.</summary>
    public DotnetExecutable Environment(string name, string value) => new(_executable.Environment(name, value));

    /// <summary>Returns defaults removing child environment variables without changing the host.</summary>
    public DotnetExecutable RemoveEnvironment(params string[] names) => new(_executable.RemoveEnvironment(names));

    /// <summary>Returns defaults with a child working directory relative to the invocation root.</summary>
    public DotnetExecutable WorkingDirectory(string path) => new(_executable.WorkingDirectory(path));

    /// <summary>Returns defaults with a positive per-process execution deadline.</summary>
    public DotnetExecutable Timeout(TimeSpan timeout) => new(_executable.Timeout(timeout));

    /// <summary>Creates a fresh untyped invocation for commands or options outside the typed surface.</summary>
    public ProcessCommand Arguments(params string[] arguments) => _executable.Arguments(arguments);

    /// <summary>Creates a fresh build command for a project, solution, file app, or the current directory.</summary>
    public DotnetBuildCommand Build(string? path = null) => new(Command("build", path));

    /// <summary>Creates a fresh publish command for a project, file app, or the current directory.</summary>
    public DotnetPublishCommand Publish(string? path = null) => new(Command("publish", path));

    /// <summary>Creates a fresh pack command for a project, solution, or the current directory.</summary>
    public DotnetPackCommand Pack(string? path = null) => new(Command("pack", path));

    private ProcessCommand Command(string verb, string? path)
    {
        if (path is null)
        {
            return _executable.Arguments(verb);
        }
        Internal.ProcessConfiguration.CheckText(path, nameof(path));
        return _executable.Arguments(verb, path);
    }
}
