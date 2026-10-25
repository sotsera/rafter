using AwesomeAssertions;
using Sotsera.Rafter;
using Xunit;
using Toolkit = Sotsera.Rafter.Rafter;

namespace Rafter.IntegrationTests.Commands;

[CollectionDefinition("Process working directory", DisableParallelization = true)]
public sealed class WorkingDirectoryCollection;

[Collection("Process working directory")]
public sealed class RuntimeHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selected_roots_and_file_paths_stay_anchored_when_a_validator_changes_cwd(bool optionBound)
    {
        var original = Directory.GetCurrentDirectory();
        var temporary = Directory.CreateTempSubdirectory("rafter-selected-root-");
        var invocation = temporary.CreateSubdirectory("invocation").FullName;
        var selected = Directory.CreateDirectory(Path.Combine(invocation, "selected")).FullName;
        var during = temporary.CreateSubdirectory("during").FullName;
        var command = Toolkit.Command("Selected root.");
        if (optionBound)
        {
            var root = command.Option<string>("root", "Root.").Default("selected");
            command.RootDirectory(root);
        }
        else
        {
            command.RootDirectory("selected");
        }
        command.Validate("Change the ambient CWD.", _ =>
        {
            Directory.SetCurrentDirectory(during);
            return true;
        });
        try
        {
            Directory.SetCurrentDirectory(invocation);
            (await command.RunAsync(["--quiet"], async context =>
            {
                context.RootDirectory.Should().Be(selected);
                Directory.GetCurrentDirectory().Should().Be(during);
                var path = context.ResolvePath("result.txt");
                await File.WriteAllTextAsync(path, "selected root", context.CancellationToken);
                File.Exists(Path.Combine(during, "result.txt")).Should().BeFalse();
                (await File.ReadAllTextAsync(path, context.CancellationToken)).Should().Be("selected root");
            })).Should().Be(0);
            Directory.GetCurrentDirectory().Should().Be(during);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            temporary.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Actual_host_root_is_captured_at_entry_and_cwd_is_never_changed_by_Rafter()
    {
        var original = Directory.GetCurrentDirectory();
        var temporary = Directory.CreateTempSubdirectory("rafter-root-");
        var before = temporary.CreateSubdirectory("before").FullName;
        var during = temporary.CreateSubdirectory("during").FullName;
        var command = Toolkit.Command("Root capture check.");
        RafterContext? captured = null;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>? run = null;
        try
        {
            Directory.SetCurrentDirectory(before);
            run = command.RunAsync(["--quiet"], async context =>
            {
                captured = context;
                await release.Task;
            });
            captured!.RootDirectory.Should().Be(before);
            Directory.GetCurrentDirectory().Should().Be(before);
            Directory.SetCurrentDirectory(during);
            captured.RootDirectory.Should().Be(before);
            release.SetResult();
            (await run).Should().Be(0);
            Directory.GetCurrentDirectory().Should().Be(during);
            (await command.RunAsync(["--quiet"], context =>
            {
                context.RootDirectory.Should().Be(during);
            })).Should().Be(0);
        }
        finally
        {
            release.TrySetResult();
            if (run is not null)
            {
                await run;
            }
            Directory.SetCurrentDirectory(original);
            temporary.Delete(recursive: true);
        }
    }
}
