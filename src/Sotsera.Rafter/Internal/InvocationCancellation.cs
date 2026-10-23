namespace Sotsera.Rafter.Internal;

internal sealed class InvocationCancellation : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _source = new();
    private readonly InvocationOutput _output;
    private Task _callbacks = Task.CompletedTask;
    private bool _stopped;
    private bool _requested;

    public CancellationToken Token => _source.Token;
    public ShutdownWindow Processes { get; } = new();

    public bool Requested
    {
        get
        {
            lock (_gate)
            {
                return _requested;
            }
        }
    }

    public InvocationCancellation(InvocationOutput output)
    {
        _output = output;
    }

    public void Request()
    {
        TaskCompletionSource notice;
        lock (_gate)
        {
            if (_requested)
            {
                if (!_stopped)
                {
                    Processes.Request(force: true);
                }
                return;
            }
            _requested = true;
            if (_stopped)
            {
                // Work has settled. Keep accepting interrupts during final output settlement, but do not
                // start user registrations or write notifications into destinations that are closing.
                return;
            }
            Processes.Request();
            notice = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _callbacks = Task.WhenAll(_source.CancelAsync(), notice.Task);
        }
        try
        {
            // A user writer may block. Do not hold the cancellation/stop-cause gate while reporting,
            // and keep the notice owned until StopAsync joins it before final output settlement.
            _output.CancellationRequested();
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            // The destination retains the failure. Never throw a reporting error on the console event thread.
        }
        finally
        {
            notice.SetResult();
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            _stopped = true;
            return _callbacks;
        }
    }

    internal bool TryCommitTimeout(ProcessDeadline deadline, Func<bool> isEmpty)
    {
        lock (_gate)
        {
            if (_requested || deadline.Stop is { Reason: not ProcessStopReason.None })
            {
                return false;
            }
            // Recheck ownership at commitment, under the same gate that accepts invocation cancellation.
            if (isEmpty())
            {
                deadline.Complete();
                return false;
            }
            if (_requested || deadline.Stop is { Reason: not ProcessStopReason.None })
            {
                return false;
            }
            deadline.Commit();
            return true;
        }
    }

    internal void CommitCaptureOverflow(ProcessStopState stop)
    {
        lock (_gate)
        {
            if (stop.Reason == ProcessStopReason.None)
            {
                stop.Commit(_requested ? ProcessStopReason.Cancellation : ProcessStopReason.CaptureLimit);
            }
        }
    }

    public void Dispose() => _source.Dispose();
}
