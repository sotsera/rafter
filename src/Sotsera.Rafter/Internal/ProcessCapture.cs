using System.Threading.Channels;

namespace Sotsera.Rafter.Internal;

internal sealed class ProcessStopState
{
    private int _reason;

    public ProcessStopReason Reason => (ProcessStopReason)Volatile.Read(ref _reason);

    // All writes are serialized by InvocationCancellation's commitment gate.
    public void Commit(ProcessStopReason reason) => Volatile.Write(ref _reason, (int)reason);
}

internal enum ProcessStopReason
{
    None,
    Timeout,
    CaptureLimit,
    Cancellation
}

internal sealed record ProcessCaptureSettings(int Limit, bool Echo);

internal sealed record ProcessCaptureSnapshot(CapturedStream Output, CapturedStream Error,
    int Limit, ProcessOutputStream? OverflowedStream, ProcessStopReason StopReason);

internal sealed class ProcessCapture
{
    private readonly object _gate = new();
    private readonly int _limit;
    private readonly InvocationCancellation _cancellation;
    private readonly ProcessStopState _stop;
    private readonly StreamBuffer _output = new();
    private readonly StreamBuffer _error = new();
    private readonly Channel<(CapturedChunk Chunk, bool Error)>? _echo;
    private long _order;
    private ProcessOutputStream? _overflowedStream;
    private ProcessCaptureSnapshot? _snapshot;

    public Task Echo { get; }

    public bool Overflowed
    {
        get
        {
            lock (_gate)
            {
                return _overflowedStream.HasValue;
            }
        }
    }

    public ProcessCapture(ProcessCaptureSettings settings, InvocationCancellation cancellation,
        ProcessStopState stop, Action<string, bool> write)
    {
        _limit = settings.Limit;
        _cancellation = cancellation;
        _stop = stop;
        if (settings.Echo)
        {
            // Only retained, nonempty chunks enter this queue: the character budget bounds its size too.
            _echo = Channel.CreateUnbounded<(CapturedChunk, bool)>(new() { SingleReader = true });
            Echo = Task.Run(async () =>
            {
                await foreach (var entry in _echo.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    write(entry.Chunk.Text, entry.Error);
                }
            });
        }
        else
        {
            Echo = Task.CompletedTask;
        }
    }

    public void Record(string text, bool error)
    {
        lock (_gate)
        {
            if (_overflowedStream.HasValue || _snapshot is not null)
            {
                return;
            }
            var buffer = error ? _error : _output;
            if (buffer.Pending is { } pending)
            {
                text = pending + text;
                buffer.Pending = null;
            }
            if (text.Length > 0 && char.IsHighSurrogate(text[^1]))
            {
                buffer.Pending = text[^1];
                text = text[..^1];
            }
            var count = Math.Min(text.Length, _limit - buffer.Length);
            if (count > 0 && count < text.Length && char.IsHighSurrogate(text[count - 1]) &&
                char.IsLowSurrogate(text[count]))
            {
                count--;
            }
            if (count > 0)
            {
                var chunk = new CapturedChunk(++_order, text[..count]);
                buffer.Chunks.Add(chunk);
                buffer.Length += count;
                _echo?.Writer.TryWrite((chunk, error));
            }
            if (count < text.Length)
            {
                _overflowedStream = error ? ProcessOutputStream.StandardError : ProcessOutputStream.StandardOutput;
                _cancellation.CommitCaptureOverflow(_stop);
            }
        }
    }

    public async Task CompleteOutputAsync(Task readers)
    {
        try
        {
            await readers.ConfigureAwait(false);
        }
        finally
        {
            _echo?.Writer.TryComplete();
            await Echo.ConfigureAwait(false);
        }
    }

    // Called only after the readers and echo consumer have joined, including failure/forced-drain paths.
    public ProcessCaptureSnapshot Snapshot()
    {
        lock (_gate)
        {
            var reason = _stop.Reason;
            if (reason == ProcessStopReason.None && _cancellation.Requested)
            {
                reason = ProcessStopReason.Cancellation;
            }
            return _snapshot ??= new(new(_output.Chunks.ToArray()), new(_error.Chunks.ToArray()),
                _limit, _overflowedStream, reason);
        }
    }

    private sealed class StreamBuffer
    {
        public List<CapturedChunk> Chunks { get; } = [];
        public int Length { get; set; }
        public char? Pending { get; set; }
    }
}
