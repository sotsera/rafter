using AwesomeAssertions;
using Xunit;

namespace Rafter.WindowsTests.Consumers;

[Trait("LongRunning", "true")]
[Collection("Native process handles")]
public sealed class ToolApiConsumerTests
{
    [WindowsTheory]
    [InlineData("file-app", "tests/testassets/consumers/runtime/file-app/probe.cs")]
    [InlineData("project", "tests/testassets/consumers/runtime/project/Consumer.csproj")]
    public async Task Tool_helpers_work_in_real_file_app_and_project_consumers(string kind, string entryPoint)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entryPoint);
        await consumer.BuildAsync();
        var result = await consumer.RunAsync("tools", "--quiet");
        result.ExitCode.Should().Be(0, result.StandardOutput + result.StandardError);
        result.StandardOutput.Should().Contain("Tool adapters passed: 10.0.");
        result.StandardError.Should().BeEmpty();
    }
}
