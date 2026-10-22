using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Processes;

public sealed class ProcessTimeoutTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Settled_timeout_preserves_exit_zero_or_unknown_and_later_cancellation(bool cancel, bool unknownExit)
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess { UnknownExit = unknownExit };
        process.OnStop = () =>
        {
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(250));
            if (cancel)
            {
                fixture.Cancellation.Request();
            }
        };
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        Func<Task> run = async () => { await fixture.RunAsync(process); };
        var timeout = (await run.Should().ThrowAsync<ProcessTimeoutException>()).Which;
        timeout.Should().BeAssignableTo<TimeoutException>();
        timeout.Timeout.Should().Be(TimeSpan.FromSeconds(1));
        timeout.Elapsed.Should().Be(TimeSpan.FromMilliseconds(1250));
        timeout.ObservedExitCode.Should().Be(unknownExit ? null : 0);
        fixture.Invocation.Failed.Should().BeFalse();
        process.Disposed.Should().BeTrue();
        fixture.Error.ToString().Should().Contain("Process deadline reached").And.NotContain("Process timed out");
    }

    [Fact]
    public async Task Cancellation_first_uses_existing_shutdown_and_never_reports_timeout()
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess();
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        fixture.Cancellation.Request();
        Func<Task> run = async () => { await fixture.RunAsync(process); };
        await run.Should().ThrowAsync<OperationCanceledException>();
        process.Disposed.Should().BeTrue();
        fixture.Error.ToString().Should().NotContain("deadline").And.NotContain("timed out");
    }

    [Fact]
    public async Task Output_failure_keeps_sticky_completion_failure_and_timeout_context()
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess { Output = new StreamReader(new FailingStream()) };
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        Func<Task> run = async () => { await fixture.RunAsync(process); };
        var failure = (await run.Should().ThrowAsync<ProcessCompletionException>()).Which;
        failure.Message.Should().Contain("Output incomplete");
        failure.Timeout.Should().Be(TimeSpan.FromSeconds(1));
        fixture.Invocation.Failed.Should().BeTrue();
        fixture.Invocation.Diagnostics.Should().ContainSingle().Which.Should().BeSameAs(failure);
        fixture.Output.Exception(failure, "body");
        fixture.Error.ToString().Should().Contain("Timeout: 1s | elapsed: 2s").And.NotContain("Process timed out");
    }

    [Fact]
    public async Task Blocked_deadline_notice_does_not_block_shutdown_and_is_joined_before_return()
    {
        using var writer = new PausedNoticeWriter();
        using var fixture = new Fixture(error: writer);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var process = new ObservedProcess { OnStop = () => stopped.TrySetResult() };
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        var run = fixture.RunAsync(process);
        try
        {
            await writer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            run.IsCompleted.Should().BeFalse("the owned notice producer still uses the destination");
        }
        finally
        {
            writer.Release.Set();
        }
        Func<Task> complete = async () => { await run; };
        await complete.Should().ThrowAsync<ProcessTimeoutException>();
    }

    [Fact]
    public async Task Already_stopped_scope_can_finish_draining_after_its_execution_deadline()
    {
        using var fixture = new Fixture();
        using var stream = new PausedStream();
        using var process = new ObservedProcess { Stopped = true, Output = new StreamReader(stream) };
        var run = fixture.RunAsync(process);
        try
        {
            await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        }
        finally
        {
            stream.Release.TrySetResult();
        }
        (await run).Succeeded.Should().BeTrue();
        fixture.Error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unhandled_timeout_rendering_keeps_exact_metadata_and_quiet_mode_hides_only_progress(bool quiet)
    {
        using var fixture = new Fixture(quiet);
        fixture.Output.ProcessDeadlineReached("tool[private]");
        fixture.Output.Exception(new ProcessTimeoutException("tool[private]", TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(10250), null), "body");
        await fixture.Output.CompleteErrorAsync();
        fixture.Error.ToString().Should().Contain("Process timed out | tool[private]")
            .And.Contain("Timeout: 10s | elapsed: 10s250ms").And.NotContain("Observed exit code")
            .And.NotContain("Exception:").And.NotContain("\u001b[");
        fixture.Error.ToString().Contains("Process deadline reached").Should().Be(!quiet);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly StringWriter _stdout = new();
        public StringWriter Error { get; }
        public ProcessDeadlineTests.ManualClock Clock { get; } = new();
        public InvocationCancellation Cancellation { get; }
        public ProcessInvocation Invocation { get; }
        public InvocationOutput Output { get; }

        public Fixture(bool quiet = false, StringWriter? error = null)
        {
            Error = error ?? new StringWriter();
            var host = new InvocationHost(Directory.GetCurrentDirectory(), _stdout, Error,
                true, true, false, 100, _ => throw new NotSupportedException());
            Output = new(host, new(false, quiet, false, true, []));
            Cancellation = new(Output);
            Invocation = new(Cancellation, Output);
        }

        public Task<Sotsera.Rafter.ProcessResult> RunAsync(IOwnedProcess process)
        {
            var settings = new ProcessSettings("tool", [], Directory.GetCurrentDirectory(),
                new Dictionary<string, string>(), [0], _ => throw new Exception("Not an exit failure"),
                TimeSpan.FromSeconds(1));
            return ProcessOperation.CompleteAsync(settings, new(Invocation, "body"), process, Clock);
        }

        public void Dispose()
        {
            Cancellation.Dispose();
            _stdout.Dispose();
            Error.Dispose();
        }
    }

    private sealed class ObservedProcess : IOwnedProcess
    {
        public StreamReader Output { get; init; } = new(new MemoryStream());
        public StreamReader Error { get; } = new(new MemoryStream());
        public long ExecutionStarted => 0;
        public int? ExitCode => Stopped && !UnknownExit ? 0 : null;
        public bool IsEmpty => Stopped;
        public bool Stopped { get; set; }
        public bool UnknownExit { get; init; }
        public bool Disposed { get; private set; }
        public Action? OnStop { get; set; }

        public bool RequestGracefulStop()
        {
            Stopped = true;
            OnStop?.Invoke();
            return true;
        }

        public void ForceStop() => Stopped = true;

        public void Dispose()
        {
            Disposed = true;
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("Output delivery failed"));
    }

    private sealed class PausedStream : MemoryStream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return 0;
        }
    }

    private sealed class PausedNoticeWriter : StringWriter
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();

        public override void Write(string? value)
        {
            if (value?.Contains("deadline", StringComparison.Ordinal) == true)
            {
                Entered.TrySetResult();
                Release.Wait();
            }
            base.Write(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Release.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
