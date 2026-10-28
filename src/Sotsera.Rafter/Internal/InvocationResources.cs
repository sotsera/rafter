using System.Collections.Concurrent;

namespace Sotsera.Rafter.Internal;

internal sealed record StateDeclaration(Type Type, Func<object>? Factory);

internal sealed record CleanupDeclaration(string? Description = null, Func<RafterContext, Task>? Callback = null,
    int Calls = 0)
{
    public void Validate(string owner, List<string> diagnostics, bool group = false)
    {
        if (Calls == 0)
        {
            return;
        }
        if (Calls > 1)
        {
            diagnostics.Add($"{owner} may declare cleanup only once.");
        }
        if (string.IsNullOrWhiteSpace(Description) || Callback is null)
        {
            diagnostics.Add($"{owner} cleanup requires a nonblank description and a callback.");
        }
        if (group)
        {
            diagnostics.Add($"{owner} requires a body to declare cleanup; groups cannot own cleanup.");
        }
    }
}

internal sealed record CleanupDiagnostic(Exception Exception, string Scope);

internal sealed class CleanupCallbackException(Exception inner) : Exception("Cleanup callback failed.", inner);

internal sealed class InvocationResources
{
    private readonly Dictionary<StateDeclaration, object> _values = new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentQueue<CleanupDiagnostic> _diagnostics = new();

    public bool Failed => !_diagnostics.IsEmpty;
    public IEnumerable<CleanupDiagnostic> Diagnostics => _diagnostics;

    public static InvocationResources Initialize(StateDeclaration[] declarations)
    {
        var resources = new InvocationResources();
        for (var index = 0; index < declarations.Length; index++)
        {
            var declaration = declarations[index];
            var value = declaration.Factory!()
                ?? throw new InvalidOperationException($"State factory {index + 1} returned null.");
            if (value is Task)
            {
                throw new InvalidOperationException(
                    $"State factory {index + 1} returned a Task. State factories must return synchronous reference state.");
            }
            resources._values.Add(declaration, value);
        }
        return resources;
    }

    public T Get<T>(State<T> state) where T : class
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!_values.TryGetValue(state.Declaration, out var value))
        {
            throw new InvalidOperationException("The state handle does not belong to this invocation's command snapshot.");
        }
        return (T)value;
    }

    public async Task CleanupAsync(CleanupDeclaration cleanup, string owner, RafterContext context,
        ProcessInvocation invocation, bool commandScope)
    {
        if (cleanup.Calls == 0)
        {
            return;
        }
        var scope = $"{owner} cleanup | {cleanup.Description}";
        if (commandScope || invocation.Cancellation.Requested)
        {
            try
            {
                // At command-scope teardown no new work callbacks may start against released resources.
                // For cancelled target work, join cancellation registrations already executing too.
                await invocation.Cancellation.StopAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
            {
                _diagnostics.Enqueue(new(exception, "Cancellation callback"));
            }
        }
        if (invocation.Diagnostics.OfType<ProcessCompletionException>().Any(failure => failure.TerminationUnknown))
        {
            _diagnostics.Enqueue(new(new InvalidOperationException(
                "Cleanup not safely attempted: owned process termination is uncertain. Retain resources."), scope));
            return;
        }
        try
        {
            invocation.Output.CleanupStarted(owner, cleanup.Description!);
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            _diagnostics.Enqueue(new(exception, scope));
        }
        try
        {
            var task = cleanup.Callback!(context)
                ?? throw new InvalidOperationException("Cleanup returned a null task.");
            await task.ConfigureAwait(false);
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            // Each callback failure is a separate occurrence, even if user code rethrows a body exception object.
            _diagnostics.Enqueue(new(new CleanupCallbackException(exception), scope));
            return;
        }
        try
        {
            invocation.Output.CleanupCompleted(owner);
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            _diagnostics.Enqueue(new(exception, scope));
        }
    }
}
