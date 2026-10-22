using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace Rafter.IntegrationTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class DurationConsumerTests
{
    [Theory]
    [InlineData("file-app", "tests/testassets/consumers/runtime/file-app/probe.cs")]
    [InlineData("project", "tests/testassets/consumers/runtime/project/Consumer.csproj")]
    public async Task Duration_handles_sources_and_exact_boundaries_work_in_both_consumer_hosts(string kind, string entry)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entry);
        await consumer.BuildAsync();
        var variable = "RAFTER_DURATION_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        consumer.EnvironmentOverrides[variable] = null;
        var defaults = await consumer.RunAsync("durations", variable, "--required", "-2m10s", "--quiet");
        VerifyValues(defaults, TimeSpan.FromSeconds(90).Ticks);
        defaults.StandardError.Should().BeEmpty();

        consumer.EnvironmentOverrides[variable] = " \t-256204778H48M5S477.5808MS ";
        var environment = await consumer.RunAsync("durations", variable, "--required=-130s", "--verbose", "--plain");
        VerifyValues(environment, long.MinValue);
        environment.StandardError.Should().Contain("--duration: environment " + variable)
            .And.Contain("--required: command line").And.Contain("--optional: not supplied")
            .And.NotContain("477.5808").And.NotContain("-130s").And.NotContain("\x1b[");

        consumer.EnvironmentOverrides[variable] = "private-invalid-duration";
        var overridden = await consumer.RunAsync("durations", variable, "-d", "+922337203685477.5807ms",
            "--required=-2m10s", "--quiet");
        VerifyValues(overridden, long.MaxValue);
        var invalidEnvironment = await consumer.RunAsync("durations", variable, "--required=-130s", "--verbose");
        invalidEnvironment.ExitCode.Should().Be(2);
        invalidEnvironment.StandardOutput.Should().BeEmpty();
        Regex.Replace(invalidEnvironment.StandardError, @"\s+", " ").Should().Contain("environment variable " + variable)
            .And.Contain("exact duration").And.NotContain("private-invalid-duration").And.NotContain("Input sources");
        consumer.EnvironmentOverrides[variable] = "";
        var empty = await consumer.RunAsync("durations", variable, "--required=-130s", "--quiet");
        empty.ExitCode.Should().Be(2);
        empty.StandardError.Should().Contain("is empty");
        foreach (var invalid in new[] { "1m1h", "1.5h30m", "0.00001ms", "", "private-invalid-duration" })
        {
            var run = await consumer.RunAsync("durations", variable, "--duration=" + invalid, "--required=-130s", "--quiet");
            run.ExitCode.Should().Be(2);
            run.StandardOutput.Should().BeEmpty();
            run.StandardError.Should().Contain("--duration").And.NotContain("private-invalid-duration");
        }
        var help = await consumer.RunAsync("durations", variable, "--help", "--plain");
        help.ExitCode.Should().Be(0);
        Regex.Replace(help.StandardOutput, @"\s+", " ").Should().Contain("--duration, -d <duration>")
            .And.Contain("Default: 1m30s.").And.Contain("Units: ms, s, m, h.").And.Contain($"Environment: {variable}.");
        help.StandardError.Should().BeEmpty();
    }

    [Fact]
    public async Task Duration_example_prints_prepared_values_and_rejects_negative_delays()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/durations.cs");
        await consumer.BuildAsync();
        consumer.EnvironmentOverrides["RAFTER_DELAY"] = null;
        var defaults = await consumer.RunAsync("--quiet");
        defaults.ExitCode.Should().Be(0);
        defaults.StandardOutput.Should().Be("Delay: 00:00:30" + Environment.NewLine
            + "Offset: not supplied" + Environment.NewLine);
        consumer.EnvironmentOverrides["RAFTER_DELAY"] = "1h90m";
        var environment = await consumer.RunAsync("--offset=-2m10s", "--quiet");
        environment.ExitCode.Should().Be(0);
        environment.StandardOutput.Should().Be("Delay: 02:30:00" + Environment.NewLine
            + "Offset: -00:02:10" + Environment.NewLine);
        consumer.EnvironmentOverrides["RAFTER_DELAY"] = "-1s";
        var invalid = await consumer.RunAsync("--quiet");
        invalid.ExitCode.Should().Be(2);
        invalid.StandardOutput.Should().BeEmpty();
        invalid.StandardError.Should().Contain("--delay (environment RAFTER_DELAY): Must be zero or greater.");
        var cli = await consumer.RunAsync("--delay=1s250ms", "--offset=0s", "--quiet");
        cli.ExitCode.Should().Be(0);
        cli.StandardOutput.Should().Be("Delay: 00:00:01.2500000" + Environment.NewLine
            + "Offset: 00:00:00" + Environment.NewLine);
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("--delay <duration>").And.Contain("Default: 30s.")
            .And.Contain("Units: ms, s, m, h.").And.Contain("Environment: RAFTER_DELAY.");
        help.StandardError.Should().BeEmpty();
    }

    private static void VerifyValues(ProcessResult result, long expectedTicks)
    {
        result.ExitCode.Should().Be(0);
        using var json = JsonDocument.Parse(result.StandardOutput);
        json.RootElement.GetProperty("DurationTicks").GetInt64().Should().Be(expectedTicks);
        json.RootElement.GetProperty("RequiredTicks").GetInt64().Should().Be(TimeSpan.FromSeconds(-130).Ticks);
        json.RootElement.GetProperty("OptionalTicks").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
