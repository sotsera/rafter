using AwesomeAssertions;
using Xunit;

namespace Rafter.IntegrationTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class ConditionalTargetConsumerTests
{
    [Theory]
    [InlineData("file-app", "tests/testassets/consumers/runtime/file-app/probe.cs")]
    [InlineData("project", "tests/testassets/consumers/runtime/project/Consumer.csproj")]
    public async Task Conditional_targets_work_in_real_consumers(string kind, string entryPoint)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entryPoint);
        await consumer.BuildAsync();
        foreach (var skip in new[] { false, true })
        {
            foreach (var pack in new[] { false, true })
            {
                var arguments = new List<string> { "conditions", "--plain" };
                if (skip)
                {
                    arguments.Add("--skip-tests");
                }
                if (pack)
                {
                    arguments.Add("--pack");
                }
                var result = await consumer.RunAsync([.. arguments]);
                result.ExitCode.Should().Be(0, result.StandardOutput + result.StandardError);
                var expected = new List<string> { "build executed" };
                if (!skip)
                {
                    expected.Add("test executed");
                }
                if (pack)
                {
                    expected.Add("pack executed");
                }
                result.StandardOutput.Should().Be(string.Join(Environment.NewLine, expected) + Environment.NewLine);
                result.StandardError.Should().Contain($"{expected.Count} target bodies completed");
                var skipped = 3 - expected.Count;
                if (skipped > 0)
                {
                    result.StandardError.Should().Contain($", {skipped} skipped");
                }
            }
        }
        var help = await consumer.RunAsync("conditions", "--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("When: Tests enabled.").And.Contain("When: Packaging requested.")
            .And.NotContain("build executed").And.NotContain("Skipped:");
        help.StandardError.Should().BeEmpty();
        var quiet = await consumer.RunAsync("conditions", "--quiet", "--skip-tests");
        quiet.ExitCode.Should().Be(0);
        quiet.StandardOutput.Should().Be("build executed" + Environment.NewLine);
        quiet.StandardError.Should().BeEmpty();
    }
}
