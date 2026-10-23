using System.Diagnostics;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.WindowsTests.Processes;

[Collection("Native process handles")]
public sealed class ProcessCaptureTests
{
    [WindowsFact]
    public async Task Abandoned_capture_is_owned_by_its_body_and_prevents_later_targets()
    {
        using var host = new Host();
        var later = false;
        Task<CapturedProcessResult>? abandoned = null;
        var first = host.Command.Target("first", "Start capture.").Run(async context =>
        {
            abandoned = Probe(context, "capture-hold", host.Ready).RunAndCaptureAsync(100);
            await WindowsProcessTests.UntilAsync(() => File.Exists(host.Ready));
        });
        var second = host.Command.Target("second", "Must not run.").DependsOn(first).Run(_ => { later = true; });
        (await host.Command.RunAsync(["--quiet"], second)).Should().Be(1);
        host.AssertStopped();
        later.Should().BeFalse();
        abandoned!.IsCompleted.Should().BeTrue();
        Func<Task> observe = async () => { await abandoned; };
        await observe.Should().ThrowAsync<OperationCanceledException>();
        host.Error.ToString().Should().Contain("unfinished process operation");
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Capture_preserves_exact_streams_and_echo_is_explicit(bool echo)
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--plain", "--quiet"], async context =>
        {
            var result = await Probe(context, "output").RunAndCaptureAsync(100, echo);
            result.StandardOutput.Text.Should().Be("  first\n\nlast");
            result.StandardError.Text.Should().Be("  error\npartial");
            result.Succeeded.Should().BeTrue();
            var chunks = result.StandardOutput.Chunks.Concat(result.StandardError.Chunks).OrderBy(chunk => chunk.Order);
            chunks.Select(chunk => chunk.Order).Should().Equal(Enumerable.Range(1, chunks.Count()).Select(value => (long)value));
        })).Should().Be(0);
        host.Output.ToString().Should().Be(echo ? "  first\n\nlast" : "");
        host.Error.ToString().Should().Be(echo ? "  error\npartial" : "");
    }

    [WindowsTheory]
    [InlineData(4, "A😀B", false)]
    [InlineData(3, "A😀", true)]
    [InlineData(2, "A", true)]
    public async Task Native_decoding_respects_UTF16_limits_and_whole_Unicode_scalars(int limit, string expected, bool overflow)
    {
        using var host = new Host();
        var observed = false;
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            try
            {
                var result = await Probe(context, "capture-unicode").RunAndCaptureAsync(limit);
                overflow.Should().BeFalse();
                result.StandardOutput.Text.Should().Be(expected);
                result.StandardError.Text.Should().Be("é世界");
            }
            catch (ProcessCaptureLimitException failure)
            {
                overflow.Should().BeTrue();
                failure.Limit.Should().Be(limit);
                failure.PartialStandardOutput.Text.Length.Should().BeLessThanOrEqualTo(limit);
                failure.PartialStandardError.Text.Length.Should().BeLessThanOrEqualTo(limit);
                // Either independent stream can overflow first; the pure buffer tests fix their observation order.
                if (failure.Stream == ProcessOutputStream.StandardOutput)
                {
                    failure.PartialStandardOutput.Text.Should().Be(expected);
                }
            }
            observed = true;
        })).Should().Be(0);
        observed.Should().BeTrue();
        host.Error.ToString().Should().BeEmpty();
    }

    [WindowsFact]
    public async Task Large_concurrent_streams_complete_without_deadlock_or_automatic_replay()
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            var result = await Probe(context, "large-output").RunAndCaptureAsync(100000);
            result.StandardOutput.Text.Should().Be(string.Concat(Enumerable.Repeat("abcdefghij", 10000)));
            result.StandardError.Text.Should().Be(string.Concat(Enumerable.Repeat("0123456789", 10000)));
        })).Should().Be(0);
        host.Output.ToString().Should().BeEmpty();
        host.Error.ToString().Should().BeEmpty();
    }

    [WindowsTheory]
    [InlineData(ProcessFailureAction.Continue, 0)]
    [InlineData(ProcessFailureAction.Fail, 1)]
    public async Task Typed_handler_returns_captured_rejection_and_preserves_invocation_behavior(
        ProcessFailureAction action, int code)
    {
        using var host = new Host();
        var calls = 0;
        CapturedProcessResult? seen = null;
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            var result = await Probe(context, "output", "7").RunAndCaptureAsync(100, onFailure: captured =>
            {
                calls++;
                seen = captured;
                captured.StandardError.Text.Should().Be("  error\npartial");
                return action;
            });
            result.Should().BeSameAs(seen);
            context.WriteLine("continued");
        })).Should().Be(code);
        calls.Should().Be(1);
        host.Output.ToString().Should().Contain("continued").And.NotContain("first");
        host.Error.ToString().Should().Contain("Capture (UTF-16 code units): stdout 13 | stderr 15")
            .And.Contain("Action: " + action).And.NotContain("partial");
    }

    [WindowsFact]
    public async Task Builder_handlers_and_default_exceptions_preserve_captured_result_and_can_recover()
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            try
            {
                await Probe(context, "output", "7").RunAndCaptureAsync(100);
                throw new InvalidOperationException("Expected rejection");
            }
            catch (ProcessFailureException failure)
            {
                var result = failure.Result.Should().BeOfType<CapturedProcessResult>().Subject;
                result.StandardOutput.Text.Should().Be("  first\n\nlast");
            }
            await Probe(context, "output", "7").OnFailure(result =>
            {
                result.Should().BeOfType<CapturedProcessResult>();
                return ProcessFailureAction.Continue;
            }).RunAndCaptureAsync(100);
        })).Should().Be(0);
        host.Error.ToString().Split("Exit code: 7").Should().HaveCount(2);
    }

    [WindowsFact]
    public async Task Per_call_settings_are_snapshots_and_repeated_calls_are_new_executions()
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            var builder = Probe(context, "output");
            var first = builder.RunAndCaptureAsync(100, onFailure: _ => throw new Exception("Accepted"));
            builder.Arguments("7").OnFailure(_ => ProcessFailureAction.Continue);
            var second = builder.RunAndCaptureAsync(100);
            var results = await Task.WhenAll(first, second);
            results[0].ExitCode.Should().Be(0);
            results[1].ExitCode.Should().Be(7);
            foreach (var result in results)
            {
                result.StandardOutput.Chunks.Concat(result.StandardError.Chunks).Min(chunk => chunk.Order).Should().Be(1);
            }
            results[0].StandardOutput.Text.Should().Be(results[1].StandardOutput.Text);
        })).Should().Be(0);
    }

    [WindowsFact]
    public async Task Overflow_stops_owned_descendants_before_catch_and_does_not_stop_unrelated_work()
    {
        using var host = new Host();
        var unrelatedReady = host.Ready + ".unrelated";
        var release = host.Ready + ".release";
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                var unrelated = Probe(context, "release", unrelatedReady, release).RunAndCaptureAsync(100);
                try
                {
                    await WindowsProcessTests.UntilAsync(() => File.Exists(unrelatedReady));
                    try
                    {
                        await Probe(context, "capture-overflow-tree", host.Ready).RunAndCaptureAsync(20);
                        throw new InvalidOperationException("Expected overflow");
                    }
                    catch (ProcessCaptureLimitException failure)
                    {
                        failure.PartialStandardOutput.Text.Should().Be(new string('x', 20));
                        host.AssertStopped();
                        unrelated.IsCompleted.Should().BeFalse();
                    }
                }
                finally
                {
                    File.WriteAllText(release, "finish");
                    (await unrelated).Succeeded.Should().BeTrue();
                }
                (await Probe(context, "exit", "0").RunAndCaptureAsync(1)).Succeeded.Should().BeTrue();
            })).Should().Be(0);
        }
        finally
        {
            File.Delete(unrelatedReady);
            File.Delete(release);
        }
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("LongRunning", "true")]
    public async Task Timeout_and_cancellation_settle_native_processes_and_keep_non_null_partials(bool cancel)
    {
        using var host = new Host();
        var run = host.Command.RunAsync(["--quiet"], async context =>
        {
            try
            {
                var builder = Probe(context, "capture-hold", host.Ready);
                if (!cancel)
                {
                    builder.Timeout(TimeSpan.FromSeconds(2));
                }
                await builder.RunAndCaptureAsync(100);
                throw new InvalidOperationException("Expected shutdown");
            }
            catch (CapturedProcessTimeoutException failure) when (!cancel)
            {
                failure.PartialStandardOutput.Text.Should().Be("child output");
                failure.PartialStandardError.Text.Should().BeEmpty();
                host.AssertStopped();
            }
            catch (CapturedProcessCanceledException failure) when (cancel)
            {
                failure.CancellationToken.Should().Be(context.CancellationToken);
                failure.PartialStandardOutput.Text.Should().Be("child output");
                host.AssertStopped();
            }
        });
        if (cancel)
        {
            await WindowsProcessTests.UntilAsync(() => File.Exists(host.Ready));
            // The probe publishes ready after writing; capture still joins the pipe reader after cancellation.
            host.Signal();
        }
        (await run).Should().Be(cancel ? 130 : 0);
        host.Error.ToString().Should().NotContain("Process timed out").And.NotContain("child output");
    }

    private static ProcessCommand Probe(RafterContext context, string mode, params string[] arguments)
        => context.Process("dotnet").Arguments(WindowsProcessTests.ProbeArguments(mode, arguments));

    private sealed class Host : IDisposable
    {
        private Action? _signal;
        public string Ready { get; } = Path.Combine(Path.GetTempPath(), "rafter-capture-" + Guid.NewGuid().ToString("N"));
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public Host()
        {
            Command = new("Capture workflow.", () => new InvocationHost(Directory.GetCurrentDirectory(), Output, Error,
                true, true, false, 100, signal =>
                {
                    _signal = signal;
                    return new Subscription(() => _signal = null);
                }));
        }

        public void Signal() => _signal!();

        public void AssertStopped()
        {
            File.Exists(Ready).Should().BeTrue();
            try
            {
                using var child = Process.GetProcessById(int.Parse(File.ReadAllText(Ready)));
                child.HasExited.Should().BeTrue();
            }
            catch (ArgumentException)
            {
                // The OS has already removed the process.
            }
        }

        public void Dispose()
        {
            TestContext.Current.TestOutputHelper?.WriteLine("stdout:\n{0}\nstderr:\n{1}", Output, Error);
            File.Delete(Ready);
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
