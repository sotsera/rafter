using AwesomeAssertions;
using Sotsera.Rafter;
using Xunit;

namespace Rafter.UnitTests.Processes;

public sealed class ExecutableConfigurationTests
{
    [Fact]
    public async Task Defaults_are_immutable_and_allow_one_explicit_override_per_child()
    {
        var command = new Command("Executable configuration.");
        (await command.RunAsync([], context =>
        {
            var original = context.Executable("must-not-launch");
            var configured = original.Environment("KEY", "default")
                .Timeout(TimeSpan.FromMinutes(1)).WorkingDirectory("default");
            var sibling = original.Environment("KEY", "sibling")
                .Timeout(TimeSpan.FromMinutes(2)).WorkingDirectory("sibling");
            sibling.Should().NotBeSameAs(configured);
            var first = configured.Arguments("one")
                .Environment("KEY", "child").Timeout(TimeSpan.FromSeconds(1)).WorkingDirectory("child");
            var second = configured.Arguments("two").RemoveEnvironment("KEY");
            second.Should().NotBeSameAs(first);
            Action duplicate = () => first.Environment("key", "again");
            duplicate.Should().Throw<InvalidOperationException>();
            Action duplicateDefault = () => configured.Timeout(TimeSpan.FromSeconds(1));
            duplicateDefault.Should().Throw<InvalidOperationException>();
        })).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_removal_is_atomic_and_conflicts_with_setting_in_the_same_layer(bool executableLayer)
    {
        var command = new Command("Atomic environment edits.");
        (await command.RunAsync([], context =>
        {
            var executable = context.Executable("must-not-launch");
            if (executableLayer)
            {
                Action invalid = () => executable.RemoveEnvironment("KEY", "BAD=NAME");
                invalid.Should().Throw<ArgumentException>();
                var removed = executable.RemoveEnvironment("KEY");
                Action conflicting = () => removed.Environment("key", "value");
                conflicting.Should().Throw<InvalidOperationException>();
                removed.Arguments().Environment("KEY", "child");
            }
            else
            {
                var child = executable.Arguments();
                Action invalid = () => child.RemoveEnvironment("KEY", "BAD=NAME");
                invalid.Should().Throw<ArgumentException>();
                child.Environment("KEY", "value");
                Action conflicting = () => child.RemoveEnvironment("key");
                conflicting.Should().Throw<InvalidOperationException>();
            }
        })).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Invalid_executable_and_typed_settings_are_rejected_before_launch(int kind)
    {
        var command = new Command("Validate executable configuration.");
        (await command.RunAsync([], context =>
        {
            Action invalid = kind switch
            {
                0 => () => context.Executable(" "),
                1 => () => context.Executable("dotnet").Environment("KEY", null!),
                2 => () => context.Dotnet().Timeout(TimeSpan.Zero),
                3 => () => context.Dotnet().Build().MaxCpuCount(0),
                4 => () => context.Dotnet().Publish().Output(" "),
                _ => () => context.Dotnet().Pack().Configuration(" ")
            };
            invalid.Should().Throw<ArgumentException>();
        })).Should().Be(0);
    }
}
