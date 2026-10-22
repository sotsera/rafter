using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace Rafter.IntegrationTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class EnvironmentConsumerTests
{
    [Theory]
    [InlineData("file-app", "tests/testassets/consumers/runtime/file-app/probe.cs")]
    [InlineData("project", "tests/testassets/consumers/runtime/project/Consumer.csproj")]
    public async Task Real_process_environment_resolves_in_both_consumer_hosts(string kind, string entryPoint)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entryPoint);
        await consumer.BuildAsync();
        var prefix = "RAFTER_TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var name = prefix + "_NAME";
        var count = prefix + "_COUNT";
        var flag = prefix + "_FLAG";
        consumer.EnvironmentOverrides[name] = "";
        consumer.EnvironmentOverrides[count] = " \t4 ";
        consumer.EnvironmentOverrides[flag] = " FALSE ";
        var empty = await consumer.RunAsync("environment", prefix, "--verbose", "--plain");
        empty.ExitCode.Should().Be(0);
        using (var result = JsonDocument.Parse(empty.StandardOutput))
        {
            result.RootElement.GetProperty("Name").GetString().Should().BeEmpty();
            result.RootElement.GetProperty("Count").GetInt32().Should().Be(4);
            result.RootElement.GetProperty("Flag").GetBoolean().Should().BeFalse();
        }
        empty.StandardError.Should().Contain($"--name: environment {name} (empty)")
            .And.Contain($"--count: environment {count}").And.NotContain("\x1b[");

        consumer.EnvironmentOverrides[name] = "  [red]Ada[/]  ";
        consumer.EnvironmentOverrides[count] = null;
        consumer.EnvironmentOverrides[flag] = null;
        var supplied = await consumer.RunAsync("environment", prefix, "--quiet");
        supplied.ExitCode.Should().Be(0);
        using (var result = JsonDocument.Parse(supplied.StandardOutput))
        {
            result.RootElement.GetProperty("Name").GetString().Should().Be("  [red]Ada[/]  ");
            result.RootElement.GetProperty("Count").GetInt32().Should().Be(3);
            result.RootElement.GetProperty("Flag").ValueKind.Should().Be(JsonValueKind.Null);
        }
        supplied.StandardError.Should().BeEmpty();

        consumer.EnvironmentOverrides[count] = "private-invalid-count";
        consumer.EnvironmentOverrides[flag] = "yes";
        var invalid = await consumer.RunAsync("environment", prefix, "--verbose");
        invalid.ExitCode.Should().Be(2);
        invalid.StandardOutput.Should().BeEmpty();
        invalid.StandardError.Should().Contain(count).And.Contain(flag).And.NotContain("private-invalid-count")
            .And.NotContain("Input sources");
        var help = await consumer.RunAsync("environment", prefix, "--help");
        help.ExitCode.Should().Be(0);
        Regex.Replace(help.StandardOutput, @"\s+", " ").Should().Contain($"Environment: {name}.")
            .And.Contain($"Environment: {flag}; requires true or false.");
        help.StandardError.Should().BeEmpty();

        var overridden = await consumer.RunAsync("environment", prefix, "-n=", "--count=0", "--flag", "--verbose");
        overridden.ExitCode.Should().Be(0);
        overridden.StandardError.Should().Contain("--name: command line").And.Contain("--count: command line")
            .And.Contain("--flag: command line").And.NotContain("environment " + prefix);

        consumer.EnvironmentOverrides[count] = null;
        consumer.EnvironmentOverrides[flag] = null;
        consumer.EnvironmentOverrides[name] = null;
        consumer.EnvironmentOverrides[name.ToLowerInvariant()] = "case-sensitive-probe";
        var casing = await consumer.RunAsync("environment", prefix, "--quiet");
        casing.ExitCode.Should().Be(OperatingSystem.IsWindows() ? 0 : 2);
        if (OperatingSystem.IsWindows())
        {
            casing.StandardOutput.Should().Contain("case-sensitive-probe");
        }
        else
        {
            casing.StandardError.Should().Contain("--name is required.");
            casing.StandardOutput.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Environment_example_runs_with_defaults_validation_and_command_line_overrides()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/environment.cs");
        await consumer.BuildAsync();
        consumer.EnvironmentOverrides["RAFTER_NAME"] = "Ada";
        consumer.EnvironmentOverrides["RAFTER_REPEAT"] = " 2 ";
        consumer.EnvironmentOverrides["RAFTER_UPPERCASE"] = "true";
        var run = await consumer.RunAsync("--quiet");
        run.ExitCode.Should().Be(0);
        run.StandardOutput.Should().Be(string.Concat(Enumerable.Repeat("HELLO, ADA!" + Environment.NewLine, 2)));
        run.StandardError.Should().BeEmpty();
        consumer.EnvironmentOverrides["RAFTER_REPEAT"] = "-1";
        var invalid = await consumer.RunAsync("--quiet");
        invalid.ExitCode.Should().Be(2);
        invalid.StandardError.Should().Contain("--repeat (environment RAFTER_REPEAT): Must be zero or greater.");
        var cli = await consumer.RunAsync("--name=", "--repeat=1", "--uppercase=false", "--quiet");
        cli.ExitCode.Should().Be(0);
        cli.StandardOutput.Should().Be("Hello, !" + Environment.NewLine);
    }
}
