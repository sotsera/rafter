using System.Text;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.IntegrationTests.Filesystem;

public sealed class FilesystemTests
{
    [Fact]
    public async Task Handles_probe_types_and_create_directories_only_when_requested()
    {
        using var workspace = new Workspace();
        (await workspace.Command.RunAsync(["--quiet"], context =>
        {
            var directory = context.Directory("nested", "evidence");
            var file = directory.File("manifest.json");
            directory.Exists().Should().BeFalse();
            file.Exists().Should().BeFalse();
            Directory.GetFileSystemEntries(workspace.Root).Should().BeEmpty();
            directory.EnsureExists().Should().BeSameAs(directory);
            directory.EnsureExists().Should().BeSameAs(directory);
            directory.Exists().Should().BeTrue();
            context.File("nested", "evidence").Exists().Should().BeFalse();
            file.Exists().Should().BeFalse();
        })).Should().Be(0, workspace.Error.ToString());
    }

    [Fact]
    public async Task Ensure_does_not_replace_a_conflicting_file()
    {
        using var workspace = new Workspace();
        var path = Path.Combine(workspace.Root, "conflict");
        await File.WriteAllTextAsync(path, "preserve", TestContext.Current.CancellationToken);
        (await workspace.Command.RunAsync(["--quiet"], context =>
        {
            Action ensure = () => context.Directory("conflict").EnsureExists();
            ensure.Should().Throw<IOException>();
            context.Directory("conflict").Exists().Should().BeFalse();
            context.File("conflict").Exists().Should().BeTrue();
        })).Should().Be(0);
        (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).Should().Be("preserve");
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("line\n\nlast")]
    [InlineData("界面 café 😀")]
    public async Task Awaited_text_writes_overwrite_exact_UTF8_bytes_without_BOM_and_close_the_file(string text)
    {
        using var workspace = new Workspace();
        (await workspace.Command.RunAsync(["--quiet"], async context =>
        {
            var directory = context.Directory("output").EnsureExists();
            var file = directory.File("result.txt");
            await File.WriteAllTextAsync(file.FullPath, new string('x', 100), context.CancellationToken);
            await file.WriteAllTextAsync(text, context.CancellationToken);
            file.Exists().Should().BeTrue();
            directory.Directory("result.txt").Exists().Should().BeFalse();
            (await File.ReadAllBytesAsync(file.FullPath, context.CancellationToken)).Should().Equal(Encoding.UTF8.GetBytes(text));
            using var exclusive = File.Open(file.FullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            exclusive.Length.Should().Be(Encoding.UTF8.GetByteCount(text));
        })).Should().Be(0, workspace.Error.ToString());
    }

    [Fact]
    public async Task Writing_does_not_create_missing_parent_directories()
    {
        using var workspace = new Workspace();
        (await workspace.Command.RunAsync(["--quiet"], async context =>
        {
            var file = context.File("missing", "result.txt");
            Func<Task> write = () => file.WriteAllTextAsync("text", context.CancellationToken);
            await write.Should().ThrowAsync<DirectoryNotFoundException>();
            context.Directory("missing").Exists().Should().BeFalse();
        })).Should().Be(0);
    }

    [Fact]
    public async Task Null_text_is_rejected_without_creating_or_truncating_a_file()
    {
        using var workspace = new Workspace();
        var path = Path.Combine(workspace.Root, "existing.txt");
        await File.WriteAllTextAsync(path, "preserve", TestContext.Current.CancellationToken);
        (await workspace.Command.RunAsync(["--quiet"], context =>
        {
            foreach (var name in new[] { "existing.txt", "missing.txt" })
            {
                Action write = () => context.File(name).WriteAllTextAsync(null!, CancellationToken.None);
                write.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("contents");
            }
            context.File("missing.txt").Exists().Should().BeFalse();
        })).Should().Be(0);
        (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).Should().Be("preserve");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Precanceled_writes_leave_existing_bytes_and_missing_paths_untouched(bool exists)
    {
        using var workspace = new Workspace();
        var path = Path.Combine(workspace.Root, "result.txt");
        if (exists)
        {
            await File.WriteAllTextAsync(path, "preserve", TestContext.Current.CancellationToken);
        }
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        (await workspace.Command.RunAsync(["--quiet"], async context =>
        {
            Func<Task> write = () => context.File("result.txt").WriteAllTextAsync("replace", cancellation.Token);
            var failure = (await write.Should().ThrowAsync<OperationCanceledException>()).Which;
            failure.CancellationToken.Should().Be(cancellation.Token);
        })).Should().Be(0);
        File.Exists(path).Should().Be(exists);
        if (exists)
        {
            (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).Should().Be("preserve");
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
    }

    [Fact]
    public async Task An_explicit_None_token_allows_the_final_write_after_invocation_cancellation()
    {
        using var workspace = new Workspace();
        (await workspace.Command.RunAsync(["--quiet"], async context =>
        {
            var file = context.File("final.json");
            workspace.Signal();
            context.CancellationToken.IsCancellationRequested.Should().BeTrue();
            await file.WriteAllTextAsync("retained evidence", CancellationToken.None);
        })).Should().Be(130);
        (await File.ReadAllTextAsync(Path.Combine(workspace.Root, "final.json"), TestContext.Current.CancellationToken))
            .Should().Be("retained evidence");
    }

    [Fact]
    public async Task Handles_can_be_shared_between_targets_and_used_after_the_invocation_with_explicit_ownership()
    {
        using var workspace = new Workspace();
        DirectoryPath? directory = null;
        var first = workspace.Command.Target("first", "Choose a directory.").Run(context =>
        {
            directory = context.Directory("shared").EnsureExists();
        });
        var second = workspace.Command.Target("second", "Write using the first target's handle.").DependsOn(first)
            .Run(async context =>
            {
                await directory!.File("during.txt").WriteAllTextAsync("during", context.CancellationToken);
            });
        (await workspace.Command.RunAsync(["--quiet"], second)).Should().Be(0);
        await directory!.File("after.txt").WriteAllTextAsync("after", CancellationToken.None);
        (await File.ReadAllTextAsync(directory.File("during.txt").FullPath, TestContext.Current.CancellationToken))
            .Should().Be("during");
        (await File.ReadAllTextAsync(directory.File("after.txt").FullPath, TestContext.Current.CancellationToken))
            .Should().Be("after");
    }

    [Fact]
    public async Task Writing_to_a_directory_propagates_an_IO_failure_and_preserves_the_directory()
    {
        using var workspace = new Workspace();
        (await workspace.Command.RunAsync(["--quiet"], async context =>
        {
            var directory = context.Directory("directory").EnsureExists();
            var file = context.File("directory");
            file.Exists().Should().BeFalse();
            Func<Task> write = () => file.WriteAllTextAsync("text", context.CancellationToken);
            var failure = (await write.Should().ThrowAsync<Exception>()).Which;
            (failure is IOException or UnauthorizedAccessException).Should().BeTrue();
            directory.Exists().Should().BeTrue();
        })).Should().Be(0);
    }

    private sealed class Workspace : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("rafter-filesystem-");
        private Action? _signal;

        public string Root => _directory.FullName;
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public Workspace()
        {
            Command = new("Filesystem I/O.", () => new InvocationHost(Root, TextWriter.Null, Error,
                true, true, false, 100, signal =>
                {
                    _signal = signal;
                    return new Subscription(() => _signal = null);
                }));
        }

        public void Signal() => _signal!();

        public void Dispose()
        {
            Error.Dispose();
            _directory.Delete(recursive: true);
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
