using System.Text;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Processes;

public sealed class ProcessCaptureTests
{
    [Fact]
    public async Task Blocked_cancellation_notice_cannot_hold_stop_commitment_and_is_joined_before_output_closes()
    {
        using var writer = new PausedCancellationWriter();
        using var fixture = new Fixture(error: writer);
        using var process = new ObservedProcess("overflow", "");
        var request = Task.Run(fixture.Cancellation.Request, TestContext.Current.CancellationToken);
        Task<Sotsera.Rafter.ProcessResult>? run = null;
        try
        {
            await writer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            run = Task.Run(() => fixture.RunAsync(process, fixture.Settings(limit: 3)),
                TestContext.Current.CancellationToken);
            Func<Task> complete = async () =>
            {
                await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            };
            var failure = (await complete.Should().ThrowAsync<CapturedProcessCanceledException>()).Which;
            failure.OverflowedStream.Should().Be(ProcessOutputStream.StandardOutput);
            failure.PartialStandardOutput.Text.Should().Be("ove");
            process.Disposed.Should().BeTrue("supervision must finish even while the notice writer is paused");
            fixture.Cancellation.StopAsync().IsCompleted.Should().BeFalse("the reporting producer is still owned");
        }
        finally
        {
            writer.Release.Set();
            await request;
            await fixture.Cancellation.StopAsync();
            if (run is not null)
            {
                try
                {
                    await run;
                }
                catch (OperationCanceledException)
                {
                    // The assertion above inspects the cancelled operation; always join it on assertion failure too.
                }
            }
        }
    }

    [Fact]
    public async Task Confirmed_cancelled_scope_can_expose_partial_data_without_an_observed_root_exit_code()
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess("retained", "") { UnknownExit = true };
        fixture.Cancellation.Request();
        Func<Task> run = async () => { await fixture.RunAsync(process, fixture.Settings()); };
        var failure = (await run.Should().ThrowAsync<CapturedProcessCanceledException>()).Which;
        failure.ObservedExitCode.Should().BeNull();
        failure.PartialStandardOutput.Text.Should().Be("retained");
        fixture.Invocation.Failed.Should().BeFalse();
    }

    [Fact]
    public async Task Overflow_first_preserves_its_exception_when_timeout_and_cancellation_follow_during_shutdown()
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess("overflow", "");
        process.OnStop = () =>
        {
            fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            fixture.Cancellation.Request();
        };
        Func<Task> run = async () => { await fixture.RunAsync(process, fixture.Settings(limit: 3)); };
        var failure = (await run.Should().ThrowAsync<ProcessCaptureLimitException>()).Which;
        failure.PartialStandardOutput.Text.Should().Be("ove");
        fixture.Cancellation.Requested.Should().BeTrue();
        fixture.StandardError.ToString().Should().Contain("Capture limit exceeded")
            .And.NotContain("deadline reached").And.NotContain("Process timed out");
        fixture.Invocation.Failed.Should().BeFalse();
    }

    [Fact]
    public async Task Failed_echo_is_sticky_output_failure_and_never_becomes_an_exit_callback()
    {
        using var writer = new FailedWriter();
        using var fixture = new Fixture(output: writer);
        using var process = new ObservedProcess("secret", "") { Stopped = true };
        Func<Task> run = async () =>
        {
            await fixture.RunAsync(process, fixture.Settings(echo: true, handler: _ => throw new Exception("Wrong path")));
        };
        var failure = (await run.Should().ThrowAsync<ProcessCompletionException>()).Which;
        failure.Capture!.Output.Text.Should().Be("secret");
        fixture.Invocation.Failed.Should().BeTrue();
        process.Disposed.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProcessFailureAction.Continue, false)]
    [InlineData(ProcessFailureAction.Fail, true)]
    [InlineData(ProcessFailureAction.Throw, false)]
    public async Task Rejected_exits_keep_the_same_typed_result_and_run_one_handler_after_settlement(
        ProcessFailureAction action, bool failed)
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess("stdout", "stderr") { Stopped = true, Code = 7 };
        CapturedProcessResult? seen = null;
        var calls = 0;
        var settings = fixture.Settings(handler: result =>
        {
            calls++;
            process.IsEmpty.Should().BeTrue();
            seen = result.Should().BeOfType<CapturedProcessResult>().Subject;
            seen.StandardOutput.Text.Should().Be("stdout");
            seen.StandardError.Text.Should().Be("stderr");
            return action;
        });
        if (action == ProcessFailureAction.Throw)
        {
            Func<Task> run = async () => { await fixture.RunAsync(process, settings); };
            var failure = (await run.Should().ThrowAsync<ProcessFailureException>()).Which;
            failure.Result.Should().BeSameAs(seen);
        }
        else
        {
            (await fixture.RunAsync(process, settings)).Should().BeSameAs(seen);
        }
        calls.Should().Be(1);
        fixture.Invocation.Failed.Should().Be(failed);
        seen!.Succeeded.Should().BeFalse();
        process.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Accepted_exit_zero_or_nonzero_skips_the_handler_and_is_silent_without_echo()
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess("private stdout", "private stderr") { Stopped = true, Code = 7 };
        var settings = fixture.Settings(handler: _ => throw new Exception("Must not run")) with { AcceptedExitCodes = [7] };
        var result = (CapturedProcessResult)await fixture.RunAsync(process, settings);
        result.Succeeded.Should().BeTrue();
        result.StandardError.Text.Should().Be("private stderr");
        fixture.StandardOutput.ToString().Should().BeEmpty();
        fixture.StandardError.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_exit_overflow_is_not_timeout_even_when_execution_deadline_has_elapsed(bool error)
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess(error ? "" : "abcd", error ? "abcd" : "") { Stopped = true };
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        Func<Task> run = async () => { await fixture.RunAsync(process, fixture.Settings(limit: 3)); };
        var failure = (await run.Should().ThrowAsync<ProcessCaptureLimitException>()).Which;
        failure.Stream.Should().Be(error ? ProcessOutputStream.StandardError : ProcessOutputStream.StandardOutput);
        failure.ObservedExitCode.Should().Be(0);
        (error ? failure.PartialStandardError.Text : failure.PartialStandardOutput.Text).Should().Be("abc");
        fixture.Invocation.Failed.Should().BeFalse();
        fixture.StandardError.ToString().Should().NotContain("deadline");
        process.Disposed.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timeout_and_cancellation_keep_shutdown_output_and_secondary_overflow(bool cancel)
    {
        using var fixture = new Fixture();
        using var stream = new ShutdownStream("final diagnostic");
        using var process = new ObservedProcess("", "") { Output = new(stream) };
        process.OnStop = () => stream.Stopped.TrySetResult();
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        if (cancel)
        {
            fixture.Cancellation.Request();
        }
        Func<Task> run = async () => { await fixture.RunAsync(process, fixture.Settings(limit: 5)); };
        if (cancel)
        {
            var failure = (await run.Should().ThrowAsync<CapturedProcessCanceledException>()).Which;
            failure.PartialStandardOutput.Text.Should().Be("final");
            failure.OverflowedStream.Should().Be(ProcessOutputStream.StandardOutput);
            failure.CancellationToken.Should().Be(fixture.Cancellation.Token);
        }
        else
        {
            var failure = (await run.Should().ThrowAsync<CapturedProcessTimeoutException>()).Which;
            failure.Should().BeAssignableTo<ProcessTimeoutException>();
            failure.PartialStandardOutput.Text.Should().Be("final");
            failure.OverflowedStream.Should().Be(ProcessOutputStream.StandardOutput);
        }
        fixture.Invocation.Failed.Should().BeFalse();
        process.Disposed.Should().BeTrue();
        fixture.StandardError.ToString().Should().NotContain("Capture limit exceeded");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Callback_failure_or_unknown_action_keeps_captured_rejection(bool invalidAction)
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess("data", "failure") { Stopped = true, Code = 9 };
        var settings = fixture.Settings(handler: _ => invalidAction ? (ProcessFailureAction)99
            : throw new IOException("Callback failed"));
        Func<Task> run = async () => { await fixture.RunAsync(process, settings); };
        var failure = (await run.Should().ThrowAsync<ProcessHandlerException>()).Which;
        var result = failure.Failure.Result.Should().BeOfType<CapturedProcessResult>().Subject;
        result.StandardError.Text.Should().Be("failure");
        failure.InnerException.Should().NotBeNull();
    }

    [Fact]
    public async Task Output_failure_keeps_sticky_completion_status_and_internal_capture_context()
    {
        using var fixture = new Fixture();
        using var process = new ObservedProcess("", "") { Output = new(new FailingStream()) };
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        Func<Task> run = async () => { await fixture.RunAsync(process, fixture.Settings()); };
        var failure = (await run.Should().ThrowAsync<ProcessCompletionException>()).Which;
        failure.Capture.Should().NotBeNull();
        failure.Message.Should().Contain("Output incomplete");
        fixture.Invocation.Failed.Should().BeTrue();
        fixture.Output.Exception(failure, "body");
        fixture.StandardError.ToString().Should().Contain("Partial capture (UTF-16 code units): stdout 0 | stderr 0")
            .And.NotContain("Process timed out");
    }

    [Fact]
    public async Task Blocked_echo_does_not_block_shutdown_but_must_join_before_returning()
    {
        using var writer = new PausedWriter();
        using var fixture = new Fixture(output: writer);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var process = new ObservedProcess("data", "") { OnStop = () => stopped.TrySetResult() };
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        var run = fixture.RunAsync(process, fixture.Settings(echo: true));
        try
        {
            await writer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            run.IsCompleted.Should().BeFalse();
        }
        finally
        {
            writer.Release.Set();
        }
        Func<Task> finish = async () => { await run; };
        var failure = (await finish.Should().ThrowAsync<CapturedProcessTimeoutException>()).Which;
        failure.PartialStandardOutput.Text.Should().Be("data");
    }

    private sealed class Fixture : IDisposable
    {
        public StringWriter StandardOutput { get; }
        public StringWriter StandardError { get; }
        public InvocationOutput Output { get; }
        public InvocationCancellation Cancellation { get; }
        public ProcessInvocation Invocation { get; }
        public ProcessDeadlineTests.ManualClock Clock { get; } = new();

        public Fixture(StringWriter? output = null, StringWriter? error = null)
        {
            StandardOutput = output ?? new StringWriter();
            StandardError = error ?? new StringWriter();
            var host = new InvocationHost(Directory.GetCurrentDirectory(), StandardOutput, StandardError,
                true, true, false, 100, _ => throw new NotSupportedException());
            Output = new(host, new(false, false, false, true, []));
            Cancellation = new(Output);
            Invocation = new(Cancellation, Output);
        }

        public ProcessSettings Settings(int limit = 100, bool echo = false,
            Func<Sotsera.Rafter.ProcessResult, ProcessFailureAction>? handler = null)
            => new("tool", [], Directory.GetCurrentDirectory(), new Dictionary<string, string>(), [0],
                handler, TimeSpan.FromSeconds(1), new(limit, echo));

        public Task<Sotsera.Rafter.ProcessResult> RunAsync(IOwnedProcess process, ProcessSettings settings)
            => ProcessOperation.CompleteAsync(settings, new(Invocation, "body"), process, Clock);

        public void Dispose()
        {
            Cancellation.Dispose();
            StandardOutput.Dispose();
            StandardError.Dispose();
        }
    }

    private sealed class ObservedProcess(string output, string error) : IOwnedProcess
    {
        public StreamReader Output { get; init; } = new(new MemoryStream(Encoding.UTF8.GetBytes(output)));
        public StreamReader Error { get; } = new(new MemoryStream(Encoding.UTF8.GetBytes(error)));
        public long ExecutionStarted => 0;
        public int? ExitCode => Stopped && !UnknownExit ? Code : null;
        public bool IsEmpty => Stopped;
        public int Code { get; init; }
        public bool UnknownExit { get; init; }
        public bool Stopped { get; set; }
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

    private sealed class ShutdownStream(string text) : MemoryStream(Encoding.UTF8.GetBytes(text))
    {
        public TaskCompletionSource Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Stopped.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("Read failed"));
    }

    private sealed class PausedWriter : StringWriter
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();

        public override void Write(string? value)
        {
            Entered.TrySetResult();
            Release.Wait();
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

    private sealed class FailedWriter : StringWriter
    {
        public override void Write(string? value) => throw new IOException("Echo failed");
    }

    private sealed class PausedCancellationWriter : StringWriter
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();

        public override void Write(string? value)
        {
            if (value?.Contains("Cancellation", StringComparison.Ordinal) == true)
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
