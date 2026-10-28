using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>Configures a named npm package script and its separate forwarded argument chunks.</summary>
public sealed class NpmRunScriptCommand : ToolCommand<NpmRunScriptCommand>
{
    private readonly object _gate = new();
    private bool _hasScriptArguments;

    internal NpmRunScriptCommand(ProcessCommand command) : base(command) { }

    /// <summary>Allows a missing script. Must precede the first ScriptArguments call.</summary>
    public NpmRunScriptCommand IfPresent()
    {
        lock (_gate)
        {
            if (_hasScriptArguments)
            {
                throw new InvalidOperationException("Configure npm options before forwarding script arguments.");
            }
            return Arguments("--if-present");
        }
    }

    /// <summary>Appends script tokens, inserting npm's -- boundary once. Does not remove npm's own script-shell semantics.</summary>
    public NpmRunScriptCommand ScriptArguments(params string[] arguments)
    {
        var copy = ProcessConfiguration.CopyArguments(arguments);
        lock (_gate)
        {
            Arguments(_hasScriptArguments ? copy : ["--", .. copy]);
            _hasScriptArguments = true;
        }
        return this;
    }
}
