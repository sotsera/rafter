namespace Sotsera.Rafter;

/// <summary>Immutable npm defaults using Node plus an explicit CLI script, without an outer shell.</summary>
public sealed class NpmExecutable
{
    private readonly Executable _node;
    private readonly string _cli;

    internal NpmExecutable(Executable node, string cli)
    {
        _node = node;
        _cli = cli;
    }

    /// <summary>Returns defaults with a child-only environment value.</summary>
    public NpmExecutable Environment(string name, string value) => new(_node.Environment(name, value), _cli);

    /// <summary>Returns defaults removing child environment variables.</summary>
    public NpmExecutable RemoveEnvironment(params string[] names) => new(_node.RemoveEnvironment(names), _cli);

    /// <summary>Returns defaults selecting the package's child working directory.</summary>
    public NpmExecutable WorkingDirectory(string path) => new(_node.WorkingDirectory(path), _cli);

    /// <summary>Returns defaults with a positive per-process execution deadline.</summary>
    public NpmExecutable Timeout(TimeSpan timeout) => new(_node.Timeout(timeout), _cli);

    /// <summary>Creates a fresh npm invocation with raw argument tokens following the CLI script path.</summary>
    public ProcessCommand Arguments(params string[] arguments)
    {
        var command = _node.Arguments(_cli);
        return command.Arguments(arguments);
    }

    /// <summary>Creates an npm install command for the package manifest in the child directory.</summary>
    public NpmInstallCommand Install() => new(Arguments("install"));

    /// <summary>Creates an npm ci command using the existing lockfile in the child directory.</summary>
    public NpmCiCommand Ci() => new(Arguments("ci"));

    /// <summary>Creates an npm run command for a named package script.</summary>
    public NpmRunScriptCommand RunScript(string name)
    {
        Internal.ProcessConfiguration.CheckText(name, nameof(name));
        if (name.StartsWith('-'))
        {
            throw new ArgumentException("A script name must not begin with '-'.", nameof(name));
        }
        return new(Arguments("run", name));
    }
}
