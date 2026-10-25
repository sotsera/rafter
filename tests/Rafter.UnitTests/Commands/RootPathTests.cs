using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Commands;

public sealed class RootPathTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Selected_roots_normalize_trailing_separators_for_literals_and_prepared_options(bool optionBound, bool absolute)
    {
        using var host = new Host();
        var expected = Path.Combine(host.Root, "selected");
        var path = (absolute ? expected : "selected") + Path.DirectorySeparatorChar;
        if (optionBound)
        {
            host.Command.RootDirectory(host.Command.Option<string>("root", "Root.").Default(path));
        }
        else
        {
            host.Command.RootDirectory(path);
        }
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            context.RootDirectory.Should().Be(expected);
            context.ResolvePath(".").Should().Be(context.RootDirectory);
        })).Should().Be(0);
    }

    [Fact]
    public async Task Selecting_a_filesystem_root_preserves_its_required_separator()
    {
        using var host = new Host();
        var root = Path.GetPathRoot(host.Root)!;
        host.Command.RootDirectory(root);
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            context.RootDirectory.Should().Be(root);
            context.ResolvePath(".").Should().Be(root);
        })).Should().Be(0);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("src/../output")]
    [InlineData("../outside")]
    [InlineData("missing/nested/file.txt")]
    public async Task Literal_roots_and_relative_paths_resolve_without_filesystem_access(string path)
    {
        using var host = new Host();
        host.Command.RootDirectory("repository");
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var root = Path.Combine(host.Root, "repository");
            context.RootDirectory.Should().Be(root);
            context.ResolvePath(path).Should().Be(Path.GetFullPath(path, root));
            var absolute = Path.Combine(Path.GetTempPath(), "elsewhere", "..", "absolute.txt");
            context.ResolvePath(absolute).Should().Be(Path.GetFullPath(absolute));
        })).Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("private\0path")]
    public async Task Invalid_resolver_arguments_are_catchable_without_losing_the_body(string? path)
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            Action resolve = () => context.ResolvePath(path!);
            resolve.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("path");
            context.ResolvePath(".").Should().Be(host.Root);
        })).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(@"C:private")]
    [InlineData(@"\private")]
    [InlineData("/private")]
    public async Task Ambiguous_Windows_rooted_paths_are_rejected_but_portable_paths_keep_platform_meaning(string path)
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            if (OperatingSystem.IsWindows())
            {
                Action resolve = () => context.ResolvePath(path);
                resolve.Should().Throw<ArgumentException>();
            }
            else
            {
                context.ResolvePath(path).Should().Be(Path.GetFullPath(path, host.Root));
            }
        })).Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("private\0path")]
    public async Task Invalid_literal_declarations_precede_help_without_exposing_the_path(string? path)
    {
        using var host = new Host();
        host.Command.RootDirectory(path!);
        var code = await host.Command.RunAsync(["--help"], _ => throw new Exception("body ran"));
        code.Should().Be(2);
        host.Output.ToString().Should().BeEmpty();
        host.Error.ToString().Should().Contain("Invalid command").And.NotContain("private").And.NotContain("body ran");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Repeated_root_configuration_is_invalid_across_both_overloads(int combination)
    {
        using var host = new Host();
        var option = host.Command.Option<string>("root", "Root.").Default(".");
        if (combination < 2)
        {
            host.Command.RootDirectory(".");
        }
        else
        {
            host.Command.RootDirectory(option);
        }
        if (combination % 2 == 0)
        {
            host.Command.RootDirectory(".");
        }
        else
        {
            host.Command.RootDirectory(option);
        }
        (await host.Command.RunAsync(["--help"], _ => { })).Should().Be(2);
        host.Error.ToString().Should().Contain("root may only be configured once");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Null_and_foreign_option_handles_are_declaration_errors(bool foreign)
    {
        using var host = new Host();
        var option = foreign ? global::Sotsera.Rafter.Rafter.Command("Other.")
            .Option<string>("root", "Root.").Default(".") : null;
        host.Command.RootDirectory(option!);
        (await host.Command.RunAsync(["--help"], _ => { })).Should().Be(2);
        host.Error.ToString().Should().Contain("root option");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("private\0path")]
    public async Task Malformed_prepared_roots_are_safe_input_errors_and_do_not_start_work(string path)
    {
        using var host = new Host();
        var root = host.Command.Option<string>("root", "Root.").Default(".");
        host.Command.RootDirectory(root);
        (await host.Command.RunAsync(["--root=" + path, "--quiet"], _ => throw new Exception("body ran")))
            .Should().Be(2);
        host.Error.ToString().Should().Contain("--root requires a valid").And.NotContain("private")
            .And.NotContain("body ran").And.NotContain("Exception");
    }

    [Fact]
    public async Task Prepared_root_uses_existing_environment_precedence_and_source_diagnostics()
    {
        using var host = new Host { EnvironmentValue = "from-environment" };
        var root = host.Command.Option<string>("root", "Root.").Default(".").FromEnvironment("ROOT_SOURCE");
        host.Command.RootDirectory(root);
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            context.RootDirectory.Should().Be(Path.Combine(host.Root, "from-environment"));
        })).Should().Be(0);
        (await host.Command.RunAsync(["--root=from-cli", "--quiet"], context =>
        {
            context.RootDirectory.Should().Be(Path.Combine(host.Root, "from-cli"));
        })).Should().Be(0);
        host.EnvironmentValue = "private\0path";
        (await host.Command.RunAsync(["--quiet"], _ => { })).Should().Be(2);
        host.Error.ToString().Should().Contain("--root (environment ROOT_SOURCE)").And.NotContain("private");
    }

    [Fact]
    public async Task Help_does_not_require_or_resolve_a_root_or_run_user_validation()
    {
        using var host = new Host();
        var root = host.Command.Option<string>("root", "Root.").Required()
            .Validate("Do not run during help.", _ => throw new Exception("validator ran"));
        host.Command.RootDirectory(root);
        (await host.Command.RunAsync(["--help", "--root=private\0path"], _ => { })).Should().Be(0);
        (await host.Command.RunAsync(["--help"], _ => { })).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
        (await host.Command.RunAsync(["--quiet"], _ => { })).Should().Be(2);
    }

    [Fact]
    public async Task User_validation_failure_prevents_root_resolution()
    {
        using var host = new Host();
        var root = host.Command.Option<string>("root", "Root.").Default("private\0path");
        host.Command.RootDirectory(root);
        host.Command.Validate("Rejected before root selection.", _ => false);
        (await host.Command.RunAsync(["--quiet"], _ => { })).Should().Be(2);
        host.Error.ToString().Should().Contain("Rejected before root selection").And.NotContain("unambiguous");
    }

    [Fact]
    public async Task Root_declaration_mutation_during_validation_affects_only_the_next_invocation()
    {
        using var host = new Host();
        var changed = false;
        host.Command.Validate("Change the next declaration snapshot.", _ =>
        {
            if (!changed)
            {
                host.Command.RootDirectory("next");
                changed = true;
            }
            return true;
        });
        (await host.Command.RunAsync(["--quiet"], context => context.RootDirectory.Should().Be(host.Root)))
            .Should().Be(0);
        (await host.Command.RunAsync(["--quiet"], context =>
            context.RootDirectory.Should().Be(Path.Combine(host.Root, "next")))).Should().Be(0);
    }

    [Fact]
    public async Task Concurrent_invocations_and_target_contexts_keep_their_prepared_roots()
    {
        using var host = new Host();
        var root = host.Command.Option<string>("root", "Root.").Default(".");
        host.Command.RootDirectory(root);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = host.Command.RunAsync(["--root=first", "--quiet"], async context =>
        {
            await release.Task;
            context.RootDirectory.Should().Be(Path.Combine(host.Root, "first"));
        });
        var target = host.Command.Target("second", "Read the second root.").Run(context =>
        {
            context.RootDirectory.Should().Be(Path.Combine(host.Root, "second"));
        });
        var second = host.Command.RunAsync(["--root=second", "--quiet"], target);
        release.SetResult();
        (await first).Should().Be(0);
        (await second).Should().Be(0);
    }

    private sealed class Host : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "rafter-path-" + Guid.NewGuid().ToString("N"));
        public string? EnvironmentValue { get; set; }
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public Host()
        {
            Command = new("Root paths.", () => new InvocationHost(Root, Output, Error, true, true, false, 100,
                _ => new Subscription()) { ReadEnvironment = _ => EnvironmentValue });
        }

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription : IDisposable
    {
        public void Dispose() { }
    }
}
