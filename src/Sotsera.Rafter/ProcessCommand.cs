using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>Configures a direct executable invocation owned by the current command or target body.</summary>
public sealed class ProcessCommand
{
    private readonly object _gate = new();
    private readonly BodyProcesses _body;
    private readonly string _executable;
    private readonly string _root;
    private readonly ExecutableDefaults _defaults;
    private readonly List<string> _arguments = [];
    private readonly Dictionary<string, string?> _environment = new(StringComparer.OrdinalIgnoreCase);
    private int[]? _acceptedExitCodes;
    private string? _directory;
    private Func<ProcessResult, ProcessFailureAction>? _handler;
    private bool _hasHandler;
    private TimeSpan? _timeout;

    internal ProcessCommand(BodyProcesses body, string executable, string root, ExecutableDefaults defaults)
    {
        ProcessConfiguration.CheckText(executable, nameof(executable));
        _body = body;
        _executable = executable;
        _root = root;
        _defaults = defaults;
    }

    /// <summary>Appends separate argument tokens, with no shell interpretation.</summary>
    public ProcessCommand Arguments(params string[] arguments)
    {
        var copy = ProcessConfiguration.CopyArguments(arguments);
        lock (_gate)
        {
            _arguments.AddRange(copy);
        }
        return this;
    }

    /// <summary>Handles a completed process with a rejected exit code. May be specified once.</summary>
    public ProcessCommand OnFailure(Func<ProcessResult, ProcessFailureAction> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (_hasHandler)
            {
                throw new InvalidOperationException("A process failure handler may only be specified once.");
            }
            _hasHandler = true;
            _handler = handler;
        }
        return this;
    }

    /// <summary>Chooses the accepted exit codes once. Repeated codes within this list are harmless.</summary>
    public ProcessCommand AcceptExitCodes(params int[] exitCodes)
    {
        ArgumentNullException.ThrowIfNull(exitCodes);
        if (exitCodes.Length == 0)
        {
            throw new ArgumentException("At least one accepted exit code is required.", nameof(exitCodes));
        }
        lock (_gate)
        {
            if (_acceptedExitCodes is not null)
            {
                throw new InvalidOperationException("Accepted exit codes may only be specified once.");
            }
            _acceptedExitCodes = exitCodes.Distinct().ToArray();
        }
        return this;
    }

    /// <summary>Chooses this child's directory once. Relative paths resolve against the captured invocation root.</summary>
    public ProcessCommand WorkingDirectory(string path)
    {
        ProcessConfiguration.CheckText(path, nameof(path));
        lock (_gate)
        {
            if (_directory is not null)
            {
                throw new InvalidOperationException("A child working directory may only be specified once.");
            }
            _directory = Path.GetFullPath(path, _root);
        }
        return this;
    }

    /// <summary>Overrides an inherited variable for this child. Each name may be configured once per child.</summary>
    public ProcessCommand Environment(string name, string value)
    {
        ProcessConfiguration.CheckEnvironment(name, value);
        lock (_gate)
        {
            ProcessConfiguration.AddEnvironment(_environment, name, value);
        }
        return this;
    }

    /// <summary>Removes variables from this child's environment. May override executable defaults once per name.</summary>
    public ProcessCommand RemoveEnvironment(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var copy = names.ToArray();
        lock (_gate)
        {
            var changes = new Dictionary<string, string?>(_environment, StringComparer.OrdinalIgnoreCase);
            foreach (var name in copy)
            {
                ProcessConfiguration.CheckEnvironmentName(name);
                ProcessConfiguration.AddEnvironment(changes, name, null);
            }
            foreach (var name in copy)
            {
                _environment.Add(name, null);
            }
        }
        return this;
    }

    /// <summary>Sets one positive execution deadline for this child and its owned descendants.
    /// Expiry begins shutdown; the operation still waits for process and output settlement.</summary>
    public ProcessCommand Timeout(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        lock (_gate)
        {
            if (_timeout.HasValue)
            {
                throw new InvalidOperationException("A process timeout may only be specified once.");
            }
            _timeout = timeout;
        }
        return this;
    }

    /// <summary>Starts this configuration and awaits its process scope, output, and failure handler.</summary>
    public Task<ProcessResult> RunAsync()
    {
        lock (_gate)
        {
            return _body.Run(Snapshot(_handler));
        }
    }

    /// <summary>Executes with bounded decoded output. The positive UTF-16 limit applies independently to each stream.
    /// The optional capture-specific rejected-exit handler cannot be combined with OnFailure.</summary>
    public Task<CapturedProcessResult> RunAndCaptureAsync(int maxCharactersPerStream, bool echo = false,
        Func<CapturedProcessResult, ProcessFailureAction>? onFailure = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharactersPerStream);
        lock (_gate)
        {
            if (_hasHandler && onFailure is not null)
            {
                throw new InvalidOperationException("Choose either OnFailure or the capture-specific handler, not both.");
            }
            Func<ProcessResult, ProcessFailureAction>? handler = onFailure is null
                ? _handler : result => onFailure((CapturedProcessResult)result);
            return _body.RunCaptured(Snapshot(handler) with { Capture = new(maxCharactersPerStream, echo) });
        }
    }

    private ProcessSettings Snapshot(Func<ProcessResult, ProcessFailureAction>? handler)
    {
        var executable = _executable.IndexOfAny(['/', '\\', ':']) >= 0
            ? Path.GetFullPath(_executable, _root) : _executable;
        var environment = new Dictionary<string, string?>(_defaults.Environment, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _environment)
        {
            environment[entry.Key] = entry.Value;
        }
        var overrides = environment.Where(entry => entry.Value is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value!, StringComparer.OrdinalIgnoreCase);
        var removed = environment.Where(entry => entry.Value is null).Select(entry => entry.Key).ToArray();
        return new(executable, _arguments.ToArray(), _directory ?? _defaults.Directory ?? _root,
            overrides, _acceptedExitCodes?.ToArray() ?? [0], handler, _timeout ?? _defaults.Timeout,
            RemovedEnvironment: removed);
    }
}
