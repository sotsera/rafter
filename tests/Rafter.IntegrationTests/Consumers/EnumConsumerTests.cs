using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace Rafter.IntegrationTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class EnumConsumerTests
{
    [Theory]
    [InlineData("file-app", "tests/testassets/consumers/runtime/file-app/probe.cs")]
    [InlineData("project", "tests/testassets/consumers/runtime/project/Consumer.csproj")]
    public async Task Consumer_defined_enums_keep_typed_handles_sources_and_safe_errors(string kind, string entry)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entry);
        await consumer.BuildAsync();
        var variable = "RAFTER_ENUM_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        consumer.EnvironmentOverrides[variable] = null;
        var defaults = await consumer.RunAsync("enums", variable, "--required=debug", "--quiet");
        VerifyValues(defaults, "Release");
        defaults.StandardError.Should().BeEmpty();

        consumer.EnvironmentOverrides[variable] = " \tDeBuG ";
        var environment = await consumer.RunAsync("enums", variable, "--required=DEBUG", "--verbose", "--plain");
        VerifyValues(environment, "Debug");
        environment.StandardError.Should().Contain("--configuration: environment " + variable)
            .And.Contain("--required: command line").And.Contain("--optional: not supplied")
            .And.NotContain("Debug").And.NotContain("Release").And.NotContain("\x1b[");

        consumer.EnvironmentOverrides[variable] = "private-invalid-enum";
        var overridden = await consumer.RunAsync("enums", variable, "-c", " RELEASE ", "--required=debug", "--quiet");
        VerifyValues(overridden, "Release");
        var invalidEnvironment = await consumer.RunAsync("enums", variable, "--required=debug", "--verbose");
        invalidEnvironment.ExitCode.Should().Be(2);
        invalidEnvironment.StandardOutput.Should().BeEmpty();
        Regex.Replace(invalidEnvironment.StandardError, @"\s+", " ").Should().Contain("environment variable " + variable)
            .And.Contain("declared enum member name").And.NotContain("private-invalid-enum").And.NotContain("Input sources");
        foreach (var invalid in new[] { "20", "Debug, Release", "", "private-invalid-enum" })
        {
            var run = await consumer.RunAsync("enums", variable, "--configuration=" + invalid, "--required=debug", "--quiet");
            run.ExitCode.Should().Be(2);
            run.StandardOutput.Should().BeEmpty();
            run.StandardError.Should().Contain("--configuration").And.NotContain("private-invalid-enum");
        }

        var help = await consumer.RunAsync("enums", variable, "--help", "--plain");
        help.ExitCode.Should().Be(0);
        var folded = Regex.Replace(help.StandardOutput, @"\s+", " ");
        folded.Should().Contain("--configuration, -c <value>").And.Contain("Default: Release.")
            .And.Contain("Allowed: Debug, Release (case-insensitive).").And.Contain($"Environment: {variable}.");
        help.StandardError.Should().BeEmpty();
    }

    [Fact]
    public async Task Enum_example_runs_with_default_environment_and_CLI_and_lists_its_choices()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/enums.cs");
        await consumer.BuildAsync();
        consumer.EnvironmentOverrides["RAFTER_CONFIGURATION"] = null;
        var defaults = await consumer.RunAsync("--quiet");
        defaults.ExitCode.Should().Be(0);
        defaults.StandardOutput.Should().Be("Selected configuration: Release." + Environment.NewLine);
        consumer.EnvironmentOverrides["RAFTER_CONFIGURATION"] = " debug ";
        var environment = await consumer.RunAsync("--quiet");
        environment.ExitCode.Should().Be(0);
        environment.StandardOutput.Should().Be("Selected configuration: Debug." + Environment.NewLine);
        var cli = await consumer.RunAsync("--configuration=RELEASE", "--quiet");
        cli.ExitCode.Should().Be(0);
        cli.StandardOutput.Should().Be(defaults.StandardOutput);
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("--configuration <value>").And.Contain("Default: Release.")
            .And.Contain("Allowed: Debug, Release (case-insensitive).");
        help.StandardError.Should().BeEmpty();
    }

    private static void VerifyValues(ProcessResult result, string expected)
    {
        result.ExitCode.Should().Be(0);
        using var json = JsonDocument.Parse(result.StandardOutput);
        json.RootElement.GetProperty("Configuration").GetString().Should().Be(expected);
        json.RootElement.GetProperty("Required").GetString().Should().Be("Debug");
        json.RootElement.GetProperty("Optional").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
