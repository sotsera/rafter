using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>Immutable executable defaults owned by the current body. Each argument call creates an independent command.</summary>
public sealed class Executable
{
    private readonly BodyProcesses _body;
    private readonly string _executable;
    private readonly string _root;
    private readonly ExecutableDefaults _defaults;

    internal Executable(BodyProcesses body, string executable, string root)
        : this(body, executable, root, ExecutableDefaults.Empty)
    {
    }

    private Executable(BodyProcesses body, string executable, string root, ExecutableDefaults defaults)
    {
        ProcessConfiguration.CheckText(executable, nameof(executable));
        _body = body;
        _executable = executable;
        _root = root;
        _defaults = defaults;
    }

    /// <summary>Creates a fresh command with separate argument tokens and these executable defaults. Does not launch.</summary>
    public ProcessCommand Arguments(params string[] arguments)
        => new ProcessCommand(_body, _executable, _root, _defaults).Arguments(arguments);

    /// <summary>Returns new defaults with this child directory. Relative paths use the invocation root.</summary>
    public Executable WorkingDirectory(string path)
    {
        ProcessConfiguration.CheckText(path, nameof(path));
        if (_defaults.Directory is not null)
        {
            throw new InvalidOperationException("An executable working directory may only be specified once.");
        }
        return With(_defaults with { Directory = Path.GetFullPath(path, _root) });
    }

    /// <summary>Returns new defaults setting a child environment variable, without changing the parent environment.</summary>
    public Executable Environment(string name, string value)
    {
        ProcessConfiguration.CheckEnvironment(name, value);
        var changes = CopyEnvironment();
        ProcessConfiguration.AddEnvironment(changes, name, value);
        return With(_defaults with { Environment = changes });
    }

    /// <summary>Returns new defaults removing these variables from child environments. Absence differs from an empty value.</summary>
    public Executable RemoveEnvironment(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var changes = CopyEnvironment();
        foreach (var name in names.ToArray())
        {
            ProcessConfiguration.CheckEnvironmentName(name);
            ProcessConfiguration.AddEnvironment(changes, name, null);
        }
        return With(_defaults with { Environment = changes });
    }

    /// <summary>Returns new defaults with a positive per-process deadline. A child can override this default once.</summary>
    public Executable Timeout(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (_defaults.Timeout.HasValue)
        {
            throw new InvalidOperationException("An executable timeout may only be specified once.");
        }
        return With(_defaults with { Timeout = timeout });
    }

    /// <summary>Creates and executes a fresh command without arguments.</summary>
    public Task<ProcessResult> RunAsync() => Arguments().RunAsync();

    /// <summary>Creates and executes a fresh zero-argument command with bounded decoded output.</summary>
    public Task<CapturedProcessResult> RunAndCaptureAsync(int maxCharactersPerStream, bool echo = false,
        Func<CapturedProcessResult, ProcessFailureAction>? onFailure = null)
        => Arguments().RunAndCaptureAsync(maxCharactersPerStream, echo, onFailure);

    private Dictionary<string, string?> CopyEnvironment()
        => new(_defaults.Environment, StringComparer.OrdinalIgnoreCase);

    private Executable With(ExecutableDefaults defaults) => new(_body, _executable, _root, defaults);
}
