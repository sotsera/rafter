using AwesomeAssertions;
using Xunit;

namespace Rafter.IntegrationTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class CleanupConsumerTests
{
    [Theory]
    [InlineData("file-app")]
    [InlineData("project")]
    public async Task Actual_cleanup_example_releases_resources_and_preserves_failures_in_fresh_consumers(string kind)
    {
        var entry = kind == "file-app" ? "examples/cleanup.cs" : "CleanupConsumer.csproj";
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entry);
        if (kind == "project")
        {
            // Compile the same example as an ordinary project, without file-app-only directives.
            var lines = await File.ReadAllLinesAsync(Path.Combine(consumer.WorkingDirectory, "examples/cleanup.cs"),
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, "CleanupProgram.cs"),
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
                    <Compile Include="CleanupProgram.cs" />
                    <ProjectReference Include="src/Sotsera.Rafter/Sotsera.Rafter.csproj" />
                  </ItemGroup>
                </Project>
                """, TestContext.Current.CancellationToken);
        }
        await consumer.BuildAsync();
        foreach (var arguments in new[]
        {
            new[] { "--plain" },
            new[] { "--plain", "--skip-verification" },
            new[] { "--plain", "--fail" },
            new[] { "--plain", "--fail-cleanup" },
            new[] { "--plain", "--fail", "--fail-cleanup" },
            new[] { "--plain", "--skip-verification", "--fail-cleanup" },
            new[] { "--quiet" }
        })
        {
            var skip = arguments.Contains("--skip-verification");
            var bodyFailure = !skip && arguments.Contains("--fail");
            var cleanupFailure = !skip && arguments.Contains("--fail-cleanup");
            var result = await consumer.RunAsync(arguments);
            result.ExitCode.Should().Be(bodyFailure || cleanupFailure ? 1 : 0, result.StandardOutput + result.StandardError);
            var expected = new List<string> { "Shared input prepared.", "Preparation resources released." };
            if (!skip)
            {
                expected.AddRange(["Verified: Shared workflow input.", "Verification resources released."]);
            }
            if (!bodyFailure && !cleanupFailure)
            {
                expected.Add("Finished with: Shared workflow input.");
            }
            expected.Add("Shared workspace released.");
            result.StandardOutput.Should().Be(string.Join(Environment.NewLine, expected) + Environment.NewLine);
            if (bodyFailure)
            {
                result.StandardError.Should().Contain("Requested verification failure.");
            }
            if (cleanupFailure)
            {
                result.StandardError.Should().Contain("Requested verification cleanup failure.")
                    .And.Contain("Target 'verify' cleanup");
            }
            if (arguments.Contains("--quiet"))
            {
                result.StandardError.Should().BeEmpty();
            }
            Directory.EnumerateDirectories(Path.Combine(consumer.WorkingDirectory, "artifacts", "work"))
                .Should().BeEmpty("all newly allocated workspaces must be released, including open verification files");
        }
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("Command cleanup:").And.Contain("Cleanup:")
            .And.NotContain("Shared input prepared.");
        help.StandardError.Should().BeEmpty();
    }
}
