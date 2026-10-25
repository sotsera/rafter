using System.Diagnostics;
using AwesomeAssertions;
using Rafter.Testing;
using Rafter.Verification;
using Xunit;

namespace Rafter.WindowsTests.Processes;

[Collection("Native process handles")]
public sealed class VerificationProcessTests
{
    [WindowsTheory]
    [InlineData("output", 13, 15)]
    [InlineData("large-output", 100000, 100000)]
    public async Task Success_drains_both_streams_including_partial_lines(string mode, int output, int error)
    {
        var result = await VerificationProcess.RunAsync(Start(mode), TimeSpan.FromSeconds(10));
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Length.Should().Be(output);
        result.StandardError.Length.Should().Be(error);
    }

    [WindowsFact]
    [Trait("LongRunning", "true")]
    public async Task Root_exit_with_a_descendant_holding_pipes_reaches_the_drain_deadline()
    {
        var ready = Path.Combine(Path.GetTempPath(), "rafter-drain-" + Guid.NewGuid().ToString("N"));
        try
        {
            var elapsed = Stopwatch.StartNew();
            Func<Task> run = async () => { await VerificationProcess.RunAsync(Start("tree", ready), TimeSpan.FromSeconds(2)); };
            var failure = (await run.Should().ThrowAsync<VerificationProcessException>()).Which;
            failure.InnerException.Should().BeOfType<TimeoutException>();
            failure.RootExited.Should().BeTrue();
            failure.StandardOutput.Should().Be("parent finished");
            failure.Message.Should().Contain("Descendant cleanup is unverified");
            elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
            // Root exit is not descendant containment. This test owns the recorded descendant separately.
            File.Exists(ready).Should().BeTrue();
        }
        finally
        {
            await StopRecordedChildAsync(ready);
        }
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("LongRunning", "true")]
    public async Task Timeout_and_cancellation_settle_a_live_root_and_preserve_partial_output(bool cancel)
    {
        var ready = Path.Combine(Path.GetTempPath(), "rafter-verify-stop-" + Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();
        try
        {
            var task = VerificationProcess.RunAsync(Start("capture-hold", ready),
                TimeSpan.FromSeconds(cancel ? 10 : 2), cancellation.Token);
            if (cancel)
            {
                await WindowsProcessTests.UntilAsync(() => File.Exists(ready));
                await cancellation.CancelAsync();
            }
            Func<Task> run = async () => { await task; };
            var failure = (await run.Should().ThrowAsync<VerificationProcessException>()).Which;
            failure.RootExited.Should().BeTrue();
            failure.StandardOutput.Should().Be("child output");
            if (cancel)
            {
                failure.InnerException.Should().BeAssignableTo<OperationCanceledException>();
            }
            else
            {
                failure.InnerException.Should().BeOfType<TimeoutException>();
            }
            IsStopped(failure.ProcessId).Should().BeTrue();
        }
        finally
        {
            await StopRecordedChildAsync(ready);
        }
    }

    [WindowsFact]
    [Trait("LongRunning", "true")]
    public async Task Consumer_retains_workspace_and_diagnostics_when_descendant_cleanup_is_uncertain()
    {
        var consumer = new IsolatedConsumer("project", TestContext.Current.TestOutputHelper!, copyWorkspace: false)
        {
            ExecutionTimeout = TimeSpan.FromSeconds(2)
        };
        var ready = Path.Combine(consumer.WorkingDirectory, "descendant.pid");
        try
        {
            Func<Task> run = async () => { await consumer.ExecuteAsync(WindowsProcessTests.ProbeArguments("tree", ready)); };
            await run.Should().ThrowAsync<VerificationProcessException>();
            consumer.Dispose();
            Directory.Exists(consumer.WorkingDirectory).Should().BeTrue();
            var evidence = Directory.GetFiles(consumer.WorkingDirectory, "verification-failure-*.txt");
            evidence.Should().ContainSingle();
            (await File.ReadAllTextAsync(evidence[0])).Should().Contain("parent finished")
                .And.Contain("Descendant cleanup is unverified");
        }
        finally
        {
            await StopRecordedChildAsync(ready);
            // Only this test's newly allocated workspace, after its recorded descendant is confirmed stopped.
            Directory.Delete(Path.GetDirectoryName(consumer.WorkingDirectory)!, recursive: true);
        }
    }

    private static ProcessStartInfo Start(string mode, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in WindowsProcessTests.ProbeArguments(mode, arguments))
        {
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    private static bool IsStopped(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static async Task StopRecordedChildAsync(string ready)
    {
        if (!File.Exists(ready))
        {
            return;
        }
        try
        {
            using var process = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(ready)));
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (ArgumentException)
        {
            // The OS has already removed this probe process.
        }
        File.Delete(ready);
    }
}
