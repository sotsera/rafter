namespace Sotsera.Rafter.Internal;

// Compare elapsed time rather than scheduling a timer with a restricted maximum duration.
internal sealed class ProcessDeadline(TimeSpan? timeout, long started, TimeProvider clock)
{
    public bool Committed { get; private set; }
    public bool ExecutionCompleted { get; private set; }
    public TimeSpan Elapsed => clock.GetElapsedTime(started);

    public bool Observe(bool empty, InvocationCancellation cancellation, Func<bool> isEmpty)
    {
        if (empty)
        {
            ExecutionCompleted = true;
        }
        if (ExecutionCompleted || Committed || timeout is null || Elapsed < timeout.Value)
        {
            return false;
        }
        return cancellation.TryCommitTimeout(this, isEmpty);
    }

    internal void Complete() => ExecutionCompleted = true;

    internal void Commit() => Committed = true;
}
