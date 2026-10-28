using AwesomeAssertions;
using Xunit;

namespace Rafter.WindowsTests.Consumers;

[Trait("LongRunning", "true")]
[Collection("Native process handles")]
public sealed class ConditionalWorkflowTests
{
    [WindowsFact]
    public async Task Actual_conditional_example_builds_tests_and_packs_a_disposable_repository()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/conditions.cs");
        // A miniature solution exercises real child tools without recursively running these outer tests.
        await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, "Rafter.slnx"), """
            <Solution>
              <Project Path="src/Sotsera.Rafter/Sotsera.Rafter.csproj" />
              <Project Path="tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj" />
            </Solution>
            """);
        await consumer.BuildAsync();
        foreach (var arguments in new[]
        {
            new[] { "--plain" },
            new[] { "--plain", "--skip-tests" },
            new[] { "--plain", "--pack", "--skip-tests" },
            new[] { "--plain", "--pack" }
        })
        {
            var result = await consumer.RunAsync(arguments);
            result.ExitCode.Should().Be(0, result.StandardOutput + result.StandardError);
            var skip = arguments.Contains("--skip-tests");
            var pack = arguments.Contains("--pack");
            if (skip)
            {
                result.StandardError.Should().Contain("[test] Skipped:").And.NotContain("[OK] test |");
                result.StandardOutput.Should().NotContain("Test run summary:");
            }
            else
            {
                result.StandardError.Should().Contain("[OK] test |");
                result.StandardOutput.Should().Contain("succeeded: 1");
            }
            if (pack)
            {
                result.StandardError.Should().Contain("[OK] pack |");
                Directory.GetFiles(Path.Combine(consumer.WorkingDirectory, "artifacts", "packages"), "*.nupkg")
                    .Should().ContainSingle();
            }
            else
            {
                result.StandardError.Should().Contain("[pack] Skipped:").And.NotContain("[OK] pack |");
                Directory.Exists(Path.Combine(consumer.WorkingDirectory, "artifacts", "packages")).Should().BeFalse();
            }
        }
        // A false test condition cannot hide a failed prerequisite or permit packing after it.
        await File.AppendAllTextAsync(Path.Combine(consumer.WorkingDirectory, "tests/testassets/workflows/src/Greeting.cs"),
            "\n#error Intentional conditional workflow failure\n");
        var failed = await consumer.RunAsync("--plain", "--pack", "--skip-tests");
        failed.ExitCode.Should().Be(1);
        failed.StandardOutput.Should().Contain("Intentional conditional workflow failure");
        failed.StandardError.Should().NotContain("[test] Skipped:").And.NotContain("[OK] pack |");
    }
}
