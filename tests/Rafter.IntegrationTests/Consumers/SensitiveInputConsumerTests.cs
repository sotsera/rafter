using AwesomeAssertions;
using Xunit;

namespace Rafter.IntegrationTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class SensitiveInputConsumerTests
{
    [Theory]
    [InlineData("file-app")]
    [InlineData("project")]
    public async Task Actual_redaction_example_preserves_sources_modes_and_failure_masking(string kind)
    {
        var entry = kind == "file-app" ? "examples/redaction.cs" : "RedactionConsumer.csproj";
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entry);
        if (kind == "project")
        {
            var lines = await File.ReadAllLinesAsync(Path.Combine(consumer.WorkingDirectory, "examples/redaction.cs"),
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, "RedactionProgram.cs"),
                "using Sotsera.Rafter;\n" + string.Join('\n', lines.Skip(4)), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, entry), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="RedactionProgram.cs" />
                    <ProjectReference Include="src/Sotsera.Rafter/Sotsera.Rafter.csproj" />
                  </ItemGroup>
                </Project>
                """, TestContext.Current.CancellationToken);
        }
        await consumer.BuildAsync();
        foreach (var arguments in new[]
        {
            new[] { "--plain" },
            new[] { "--quiet" },
            new[] { "--verbose" },
            new[] { "--plain", "--token", "cli-demo-token", "--secondary-token", "second-demo-token" },
            new[] { "--plain", "--fail" },
            new[] { "--quiet", "--fail" },
            new[] { "--verbose", "--fail" },
            new[] { "--token=short" },
            new[] { "--token=" }
        })
        {
            var useEnvironment = arguments.Contains("--verbose");
            consumer.EnvironmentOverrides["RAFTER_TOKEN"] = useEnvironment ? "env-demo-token" : null;
            consumer.EnvironmentOverrides["RAFTER_SECONDARY_TOKEN"] = null;
            var invalid = arguments.Any(argument => argument.StartsWith("--token=", StringComparison.Ordinal));
            var fail = arguments.Contains("--fail");
            var result = await consumer.RunAsync(arguments);
            result.ExitCode.Should().Be(invalid ? 2 : fail ? 1 : 0, result.StandardOutput + result.StandardError);
            (result.StandardOutput + result.StandardError).Should().NotContain("demo-token-only")
                .And.NotContain("cli-demo-token").And.NotContain("second-demo-token").And.NotContain("env-demo-token");
            if (invalid)
            {
                result.StandardOutput.Should().BeEmpty();
                result.StandardError.Should().Contain("Use at least eight characters.");
                continue;
            }
            var expected = "Authorization: Bearer [REDACTED]" + Environment.NewLine;
            if (arguments.Contains("--secondary-token"))
            {
                expected += "X-Secondary-Token: [REDACTED]" + Environment.NewLine;
            }
            if (!fail)
            {
                expected += "Request prepared; no network request was sent." + Environment.NewLine;
            }
            result.StandardOutput.Should().Be(expected);
            if (fail)
            {
                result.StandardError.Should().Contain("Example request failed for Bearer [REDACTED].");
            }
            else if (arguments.Contains("--quiet"))
            {
                result.StandardError.Should().BeEmpty();
            }
        }
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("Sensitive. Default: [REDACTED].")
            .And.Contain("RAFTER_TOKEN").And.NotContain("demo-token-only").And.NotContain("env-demo-token");
        help.StandardError.Should().BeEmpty();
    }
}
