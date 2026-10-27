using System.Diagnostics;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.WindowsTests.Processes;

[Collection("Native process handles")]
[Trait("LongRunning", "true")]
public sealed class ProcessTimeoutTests
{
    [WindowsTheory]
    [InlineData("")]
    [InlineData("--quiet")]
    [InlineData("--plain")]
    [InlineData("--verbose")]
    public async Task Unhandled_timeout_settles_native_child_before_finally_and_reports_one_failure(string control)
    {
        using var host = new Host();
        var cleaned = false;
        var continued = false;
        var handler = false;
        var code = await host.Command.RunAsync(control.Length == 0 ? [] : [control], async context =>
        {
            try
            {
                await Probe(context, "hold", host.Ready).Timeout(TimeSpan.FromSeconds(2)).OnFailure(_ =>
                {
                    handler = true;
                    return ProcessFailureAction.Continue;
                }).RunAsync();
                continued = true;
            }
            finally
            {
                host.AssertStopped();
                cleaned = true;
            }
        });
        code.Should().Be(1);
        cleaned.Should().BeTrue();
        continued.Should().BeFalse();
        handler.Should().BeFalse();
        host.Error.ToString().Should().Contain("Timeout: 2s | elapsed:")
            .And.Contain("Observed exit code:").And.NotContain("ProcessTimeoutException:");
        host.Error.ToString().Split("Process timed out | dotnet").Should().HaveCount(2);
        host.Error.ToString().Contains("Process deadline reached").Should().Be(control != "--quiet");
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Catch_can_launch_fresh_work_and_run_later_targets_but_cannot_erase_prior_fail(bool fail)
    {
        using var host = new Host();
        var later = false;
        var build = host.Command.Target("build", "Build.").Run(async context =>
        {
            if (fail)
            {
                await Probe(context, "exit", "7").OnFailure(_ => ProcessFailureAction.Fail).RunAsync();
            }
            try
            {
                await Probe(context, "hold", host.Ready).Timeout(TimeSpan.FromSeconds(2)).RunAsync();
            }
            catch (ProcessTimeoutException timeout)
            {
                timeout.Executable.Should().Be("dotnet");
                timeout.Elapsed.Should().BeGreaterThanOrEqualTo(timeout.Timeout);
                host.AssertStopped();
                var result = await Probe(context, "exit", "0").Timeout(TimeSpan.MaxValue).RunAsync();
                result.Succeeded.Should().BeTrue();
                context.WriteLine("Recovered.");
            }
        });
        var test = host.Command.Target("test", "Test.").DependsOn(build).Run(_ => { later = true; });
        (await host.Command.RunAsync([], test)).Should().Be(fail ? 1 : 0);
        later.Should().Be(!fail);
        host.Output.ToString().Should().Contain("Recovered.");
        host.Error.ToString().Should().Contain("Process deadline reached").And.NotContain("Process timed out");
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_after_settled_timeout_preserves_failure_or_caught_cancellation(bool caught)
    {
        using var host = new Host();
        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = host.Command.RunAsync([], async context =>
        {
            try
            {
                await Probe(context, "hold", host.Ready).Timeout(TimeSpan.FromSeconds(2)).RunAsync();
            }
            catch (ProcessTimeoutException)
            {
                host.AssertStopped();
                expired.TrySetResult();
                await release.Task;
                if (!caught)
                {
                    throw;
                }
            }
        });
        try
        {
            await expired.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            host.Signal();
        }
        finally
        {
            release.TrySetResult();
        }
        (await run).Should().Be(caught ? 130 : 1);
        host.Error.ToString().Contains("Process timed out").Should().Be(!caught);
    }

    [WindowsFact]
    public async Task One_deadline_does_not_stop_an_unrelated_concurrent_owned_process()
    {
        using var host = new Host();
        var ready = host.Ready + ".unrelated";
        var release = host.Ready + ".release";
        try
        {
            (await host.Command.RunAsync([], async context =>
            {
                var unrelated = Probe(context, "release", ready, release).RunAsync();
                try
                {
                    await WindowsProcessTests.UntilAsync(() => File.Exists(ready));
                    try
                    {
                        await Probe(context, "hold", host.Ready).Timeout(TimeSpan.FromTicks(1)).RunAsync();
                    }
                    catch (ProcessTimeoutException)
                    {
                        unrelated.IsCompleted.Should().BeFalse();
                        using var child = Process.GetProcessById(int.Parse(File.ReadAllText(ready)));
                        child.HasExited.Should().BeFalse();
                    }
                }
                finally
                {
                    File.WriteAllText(release, "finish");
                    (await unrelated).Succeeded.Should().BeTrue();
                }
            })).Should().Be(0);
        }
        finally
        {
            File.Delete(ready);
            File.Delete(release);
        }
    }

    [WindowsFact]
    public async Task Adding_timeout_after_run_does_not_change_the_running_snapshot_and_cancellation_wins()
    {
        using var host = new Host();
        var run = host.Command.RunAsync([], async context =>
        {
            var builder = Probe(context, "hold", host.Ready);
            var operation = builder.RunAsync();
            try
            {
                await WindowsProcessTests.UntilAsync(() => File.Exists(host.Ready));
                builder.Timeout(TimeSpan.FromTicks(1));
                await Task.Delay(200, TestContext.Current.CancellationToken);
                operation.IsCompleted.Should().BeFalse("the running launch captured no deadline");
            }
            finally
            {
                host.Signal();
                await operation;
            }
        });
        (await run).Should().Be(130);
        host.AssertStopped();
        host.Error.ToString().Should().NotContain("deadline").And.NotContain("timed out");
    }

    [WindowsFact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task Timeout_still_applies_to_owned_descendants_after_root_exit_zero()
    {
        using var host = new Host();
        var native = WindowsProcess.Start("dotnet", WindowsProcessTests.ProbeArguments("tree-silent", host.Ready),
            Directory.GetCurrentDirectory(), new Dictionary<string, string>());
        using (native)
        {
            await WindowsProcessTests.UntilAsync(() => native.ExitCode.HasValue);
            native.ExitCode.Should().Be(0);
            native.IsEmpty.Should().BeFalse();
            File.Exists(host.Ready).Should().BeTrue();
            var output = new InvocationOutput(host.InvocationHost, new(false, false, false, true, []));
            using var cancellation = new InvocationCancellation(output);
            var invocation = new ProcessInvocation(cancellation, output);
            var settings = new ProcessSettings("dotnet", [], Directory.GetCurrentDirectory(),
                new Dictionary<string, string>(), [0], null, TimeSpan.FromSeconds(1));
            var clock = new ExpiredClock(native.ExecutionStarted);
            Func<Task> run = async () =>
            {
                await ProcessOperation.CompleteAsync(settings, new(invocation, "body"), native, clock);
            };
            var timeout = (await run.Should().ThrowAsync<ProcessTimeoutException>()).Which;
            timeout.ObservedExitCode.Should().Be(0);
            invocation.Failed.Should().BeFalse();
            host.AssertStopped();
            host.Output.ToString().Should().BeEmpty();
        }
    }

    private static ProcessCommand Probe(RafterContext context, string mode, params string[] arguments)
        => context.Executable("dotnet").Arguments(WindowsProcessTests.ProbeArguments(mode, arguments));

    private sealed class ExpiredClock(long started) : TimeProvider
    {
        public override long GetTimestamp() => started + Stopwatch.Frequency * 2;
    }

    private sealed class Host : IDisposable
    {
        private Action? _signal;
        public string Ready { get; } = Path.Combine(Path.GetTempPath(), "rafter-timeout-" + Guid.NewGuid().ToString("N"));
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public InvocationHost InvocationHost { get; }
        public Command Command { get; }

        public Host()
        {
            InvocationHost = new(Directory.GetCurrentDirectory(), Output, Error, true, true, false, 100, signal =>
            {
                _signal = signal;
                return new Subscription(() => _signal = null);
            });
            Command = new("Timeout workflow.", () => InvocationHost);
        }

        public void Signal() => _signal!();

        public void AssertStopped()
        {
            if (File.Exists(Ready))
            {
                try
                {
                    using var child = Process.GetProcessById(int.Parse(File.ReadAllText(Ready)));
                    child.HasExited.Should().BeTrue();
                }
                catch (ArgumentException)
                {
                    // The OS has already removed the stopped process.
                }
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
