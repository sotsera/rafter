using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>The invocation's prepared input values, output, and cooperative cancellation scope.</summary>
public sealed class RafterContext
{
    private readonly InvocationOutput _output;
    private readonly InputValues _values;
    private readonly ProcessInvocation _invocation;
    private readonly BodyProcesses? _processes;
    private readonly InvocationResources? _resources;

    /// <summary>Signals that the invocation should stop and finish its cleanup.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>The absolute path base selected for this invocation, independent of later working-directory changes.</summary>
    public string RootDirectory { get; }
    internal bool ProcessFailure => _invocation.Failed;
    internal bool ExecutionFailed => ProcessFailure || _resources?.Failed == true;

    internal RafterContext(string rootDirectory, InvocationOutput output, CancellationToken cancellationToken,
        InputValues values, ProcessInvocation invocation, BodyProcesses? processes = null,
        InvocationResources? resources = null)
    {
        RootDirectory = rootDirectory;
        _output = output;
        _values = values;
        CancellationToken = cancellationToken;
        _invocation = invocation;
        _processes = processes;
        _resources = resources;
    }

    /// <summary>Writes a literal result line to stdout, preserving embedded newlines and indentation.
    /// Known sensitive input values are masked; direct writes and child output are outside this boundary.</summary>
    public void WriteLine(string? text = null) => _output.WriteLine(text);

    /// <summary>Resolves a relative path against this invocation's root, or normalizes a fully qualified path.
    /// Performs no filesystem I/O and does not require the path to exist.</summary>
    public string ResolvePath(string path) => RootPaths.Resolve(path, RootDirectory);

    /// <summary>Combines path segments and selects a directory relative to this invocation's root without performing I/O.</summary>
    public DirectoryPath Directory(params string[] paths) => new(RootPaths.Combine(RootDirectory, paths));

    /// <summary>Combines path segments and selects a file relative to this invocation's root without performing I/O.</summary>
    public FilePath File(params string[] paths) => new(RootPaths.Combine(RootDirectory, paths));

    /// <summary>Configures a reusable executable owned by this body, with the invocation root as its default directory.</summary>
    public Executable Executable(string executable)
    {
        return new(_processes ?? throw new InvalidOperationException("A process requires an executing body."),
            executable, RootDirectory);
    }

    internal async Task RunBodyAsync(Func<RafterContext, Task> body, string name)
    {
        var processes = new BodyProcesses(_invocation, name);
        var context = new RafterContext(RootDirectory, _output, CancellationToken, _values, _invocation, processes,
            _resources);
        try
        {
            var task = body(context) ?? throw new InvalidOperationException($"{name} returned a null task.");
            await task.ConfigureAwait(false);
        }
        finally
        {
            await processes.FinishAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Returns this invocation's state for a handle declared on its command. Does not call the factory.</summary>
    public T GetState<T>(State<T> state) where T : class
    {
        ArgumentNullException.ThrowIfNull(state);
        return (_resources ?? throw new InvalidOperationException("State is unavailable outside an initialized invocation."))
            .Get(state);
    }

    internal Task RunCleanupAsync(CleanupDeclaration cleanup, string owner, bool commandScope = false)
    {
        if (cleanup.Calls == 0)
        {
            return Task.CompletedTask;
        }
        var context = new RafterContext(RootDirectory, _output, CancellationToken.None, _values, _invocation,
            resources: _resources);
        return _resources!.CleanupAsync(cleanup, owner, context, _invocation, commandScope);
    }

    /// <summary>Returns the value prepared before execution for a required or defaulted input.</summary>
    public T GetValue<T>(ValueOption<T> option) => _values.GetRequired(option);

    /// <summary>Returns the supplied string, or null when the optional input was omitted.</summary>
    public string? GetValue(Option<string> option) => _values.GetString(option);

    /// <summary>Returns the supplied value, or null when the optional input was omitted.</summary>
    public T? GetValue<T>(Option<T> option) where T : struct => _values.GetOptional(option);
}
