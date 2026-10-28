using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Processes;

public sealed class ToolConfigurationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Invalid_tool_configuration_throws_before_starting_work(int kind)
    {
        (await global::Sotsera.Rafter.Rafter.Command("Validate tool commands.").RunAsync([], context =>
        {
            Action configure = kind switch
            {
                0 => () => context.Git().Log().MaxCount(0),
                1 => () => context.Npm("node", "npm-cli.js").RunScript(" "),
                2 => () => context.Npm("node", "npm-cli.js").RunScript("--help"),
                3 => () => context.Npm("node", " "),
                4 => () => context.Git().Timeout(TimeSpan.Zero),
                _ => () => context.Dotnet().UseWorkingDirectorySdk().RemoveEnvironment("MSBuildSDKsPath")
            };
            configure.Should().Throw<Exception>();
        })).Should().Be(0);
    }

    [Fact]
    public async Task Script_argument_validation_is_atomic_and_npm_options_precede_the_boundary()
    {
        (await global::Sotsera.Rafter.Rafter.Command("Validate script argument boundary.").RunAsync([], context =>
        {
            var script = context.Npm("node", "npm-cli.js").RunScript("build");
            Action invalid = () => script.ScriptArguments("valid", null!);
            invalid.Should().Throw<ArgumentException>();
            script.IfPresent().ScriptArguments("--mode", "production").ScriptArguments("value");
            Action lateOption = () => script.IfPresent();
            lateOption.Should().Throw<InvalidOperationException>();
            context.Npm("node", "npm-cli.js").RunScript("build").IfPresent();
        })).Should().Be(0);
    }

    [Fact]
    public async Task Tool_defaults_are_immutable_and_child_overrides_remain_independent()
    {
        (await global::Sotsera.Rafter.Rafter.Command("Tool defaults.").RunAsync([], context =>
        {
            var git = context.Git();
            var configuredGit = git.Environment("KEY", "one").Timeout(TimeSpan.FromMinutes(1));
            git.Environment("KEY", "two").Timeout(TimeSpan.FromMinutes(2));
            configuredGit.Status().Environment("KEY", "child").Timeout(TimeSpan.FromSeconds(1));
            var npm = context.Npm("node", "npm-cli.js");
            var configuredNpm = npm.Environment("KEY", "one").Timeout(TimeSpan.FromMinutes(1));
            npm.Environment("KEY", "two").Timeout(TimeSpan.FromMinutes(2));
            configuredNpm.Ci().Environment("KEY", "child").Timeout(TimeSpan.FromSeconds(1));
        })).Should().Be(0);
    }

}
