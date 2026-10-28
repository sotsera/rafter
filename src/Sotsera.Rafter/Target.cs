using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>A named body or dependency-only group declared on a command.</summary>
public sealed class Target
{
    private readonly object _gate;
    private readonly string _name;
    private readonly string _description;
    private readonly List<Target> _dependencies = [];
    private Func<RafterContext, Task>? _body;
    private int _bodyCalls;
    private bool _nullDependencies;
    private string? _conditionDescription;
    private Func<RafterContext, bool>? _condition;
    private int _conditionCalls;

    internal Target(object gate, string name, string description)
    {
        _gate = gate;
        _name = name;
        _description = description;
    }

    /// <summary>Appends prerequisites in traversal order. Repeated references execute only once.</summary>
    public Target DependsOn(params Target[] dependencies)
    {
        lock (_gate)
        {
            if (dependencies is null)
            {
                _nullDependencies = true;
            }
            else
            {
                _dependencies.AddRange(dependencies);
            }
        }
        return this;
    }

    /// <summary>Runs this target's body only when the described synchronous condition is true.
    /// Evaluated once after prerequisites; false skips only the body and allows dependants to continue.
    /// Invalid, repeated, or group conditions are rejected by RunAsync before work.</summary>
    public Target When(string description, Func<RafterContext, bool> predicate)
    {
        lock (_gate)
        {
            _conditionCalls++;
            _conditionDescription = description;
            _condition = predicate;
        }
        return this;
    }

    /// <summary>Registers a synchronous body. Null or repeated body declarations are rejected by RunAsync.</summary>
    public Target Run(Action<RafterContext> body)
    {
        return Run(body is null ? (Func<RafterContext, Task>)null! : context =>
        {
            body(context);
            return Task.CompletedTask;
        });
    }

    /// <summary>Registers an awaited body. A group can omit a body when it has dependencies.</summary>
    public Target Run(Func<RafterContext, Task> body)
    {
        lock (_gate)
        {
            _bodyCalls++;
            _body = body;
        }
        return this;
    }

    internal TargetSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new(this, _name, _description, _dependencies.Distinct().ToArray(), _body,
                _bodyCalls, _nullDependencies, _conditionDescription, _condition, _conditionCalls);
        }
    }
}
