using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Commands;

public sealed class FilesystemPathTests
{
    [Fact]
    public async Task Multi_segment_handles_are_immutable_absolute_values_and_create_nothing()
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var parts = new[] { "artifacts", "run" };
            var directory = context.Directory(parts);
            var expected = Path.Combine(host.Root, "artifacts", "run");
            directory.FullPath.Should().Be(expected);
            parts[1] = "changed";
            directory.FullPath.Should().Be(expected);
            directory.Directory("nested", "..").FullPath.Should().Be(expected);
            directory.File("logs", "result.txt").FullPath.Should().Be(Path.Combine(expected, "logs", "result.txt"));
            context.File("artifacts", "run", "result.txt").FullPath.Should().Be(Path.Combine(expected, "result.txt"));
            context.RootDirectory.Should().Be(host.Root);
            Directory.Exists(host.Root).Should().BeFalse();
        })).Should().Be(0);
    }

    [Fact]
    public async Task Absolute_segments_replace_the_previous_base_for_all_factories()
    {
        using var host = new Host();
        var absolute = Path.Combine(Path.GetTempPath(), "rafter-absolute-" + Guid.NewGuid().ToString("N"));
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var directory = context.Directory("ignored");
            var expected = Path.Combine(absolute, "end");
            context.Directory("ignored", absolute, "end").FullPath.Should().Be(expected);
            context.File("ignored", absolute, "end").FullPath.Should().Be(expected);
            directory.Directory("ignored", absolute, "end").FullPath.Should().Be(expected);
            directory.File("ignored", absolute, "end").FullPath.Should().Be(expected);
            Directory.Exists(absolute).Should().BeFalse();
        })).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Invalid_arrays_and_segments_are_rejected_by_all_factories(int invalid)
    {
        using var host = new Host();
        string[]? paths = invalid switch
        {
            0 => null,
            1 => [],
            2 => [""],
            3 => [" \t"],
            4 => ["private\0path"],
            _ => [null!]
        };
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var parent = context.Directory("parent");
            Action[] factories = [() => context.Directory(paths!), () => context.File(paths!),
                () => parent.Directory(paths!), () => parent.File(paths!)];
            foreach (var factory in factories)
            {
                factory.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("paths");
            }
            Directory.Exists(host.Root).Should().BeFalse();
        })).Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("private\0path")]
    public async Task An_absolute_segment_does_not_hide_an_invalid_earlier_segment(string? invalid)
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var parent = context.Directory("parent");
            var absolute = Path.GetPathRoot(host.Root)!;
            Action[] factories = [() => context.Directory(invalid!, absolute), () => context.File(invalid!, absolute),
                () => parent.Directory(invalid!, absolute), () => parent.File(invalid!, absolute)];
            foreach (var factory in factories)
            {
                factory.Should().Throw<ArgumentException>();
            }
        })).Should().Be(0);
    }

    [Theory]
    [InlineData(@"C:private")]
    [InlineData(@"\private")]
    [InlineData("/private")]
    public async Task Windows_ambiguous_segments_are_validated_before_an_absolute_override(string path)
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var absolute = Path.GetPathRoot(host.Root)!;
            if (OperatingSystem.IsWindows())
            {
                Action resolve = () => context.Directory(path, absolute);
                resolve.Should().Throw<ArgumentException>();
            }
            else
            {
                context.Directory(path, absolute).FullPath.Should().Be(absolute);
            }
        })).Should().Be(0);
    }

    [Fact]
    public async Task Directory_handles_normalize_trailing_separators_and_preserve_filesystem_roots()
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            context.Directory("folder/").FullPath.Should().Be(Path.Combine(host.Root, "folder"));
            context.Directory(Path.GetPathRoot(host.Root)!).FullPath.Should().Be(Path.GetPathRoot(host.Root));
        })).Should().Be(0);
    }

    private sealed class Host : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "rafter-fs-path-" + Guid.NewGuid().ToString("N"));
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public Host()
        {
            Command = new("Filesystem paths.", () => new InvocationHost(Root, TextWriter.Null, Error,
                true, true, false, 100, _ => new Subscription()));
        }

        public void Dispose() => Error.Dispose();
    }

    private sealed class Subscription : IDisposable
    {
        public void Dispose() { }
    }
}
