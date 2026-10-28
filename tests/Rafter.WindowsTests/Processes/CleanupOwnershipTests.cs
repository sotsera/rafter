using System.Diagnostics;
using AwesomeAssertions;
using Sotsera.Rafter;
using Xunit;

namespace Rafter.WindowsTests.Processes;

[Collection("Native process handles")]
[Trait("LongRunning", "true")]
public sealed class CleanupOwnershipTests
{
    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_runs_only_after_real_child_and_output_settlement_even_for_abandoned_work(bool abandon)
    {
        var temporary = Directory.CreateTempSubdirectory("rafter-cleanup-owned-");
        using var host = new ToolTestHost(temporary.FullName);
        var ready = Path.Combine(temporary.FullName, "ready.txt");
        var release = Path.Combine(temporary.FullName, "release.txt");
        var targetReleased = false;
        var commandReleased = false;
        try
        {
            host.Command.Cleanup("Release command resources.", _ =>
            {
                targetReleased.Should().BeTrue();
                commandReleased = true;
            });
            var target = host.Command.Target("work", "Run an owned child.").Run(async context =>
            {
                var running = context.Executable("dotnet")
                    .Arguments(WindowsProcessTests.ProbeArguments("release", ready, release))
                    .Timeout(TimeSpan.FromSeconds(30))
                    .RunAndCaptureAsync(4096);
                var deadline = Stopwatch.StartNew();
                while (!File.Exists(ready) && deadline.Elapsed < TimeSpan.FromSeconds(10))
                {
                    await Task.Delay(20, TestContext.Current.CancellationToken);
                }
                File.Exists(ready).Should().BeTrue("the real child must enter before testing cleanup");
                if (!abandon)
                {
                    await File.WriteAllTextAsync(release, "release", context.CancellationToken);
                    await running;
                }
                // In the abandoned case the existing owner must stop/join the operation before cleanup runs.
            }).Cleanup("Release target resources.", _ =>
            {
                var id = int.Parse(File.ReadAllText(ready));
                try
                {
                    using var process = Process.GetProcessById(id);
                    process.HasExited.Should().BeTrue();
                }
                catch (ArgumentException)
                {
                    // The recorded child no longer exists.
                }
                File.Delete(ready);
                targetReleased = true;
            });
            (await host.Command.RunAsync(["--quiet"], target)).Should().Be(abandon ? 1 : 0, host.Error.ToString());
            targetReleased.Should().BeTrue();
            commandReleased.Should().BeTrue();
            if (abandon)
            {
                host.Error.ToString().Should().Contain("unfinished process operation");
            }
        }
        finally
        {
            if (targetReleased)
            {
                temporary.Delete(true);
            }
            else
            {
                TestContext.Current.TestOutputHelper?.WriteLine(
                    "Retained verification workspace because child settlement was not confirmed: " + temporary.FullName);
            }
        }
    }
}
