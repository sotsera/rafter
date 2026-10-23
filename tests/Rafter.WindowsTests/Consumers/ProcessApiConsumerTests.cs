using AwesomeAssertions;
using Xunit;

namespace Rafter.WindowsTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class ProcessApiConsumerTests
{
    [WindowsTheory]
    [InlineData("file-app", "tests/testassets/consumers/runtime/file-app/probe.cs")]
    [InlineData("project", "tests/testassets/consumers/runtime/project/Consumer.csproj")]
    public async Task Public_process_api_works_in_real_consumers(string kind, string entryPoint)
    {
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entryPoint);
        await consumer.BuildAsync();
        var process = await consumer.RunAsync("process", "--quiet");
        process.ExitCode.Should().Be(0);
        process.StandardOutput.Should().Contain("10.0.").And.Contain("Process result: 0:True");
        process.StandardError.Should().BeEmpty();

        var capture = await consumer.RunAsync("capture", "--quiet");
        capture.ExitCode.Should().Be(0);
        capture.StandardOutput.Should().Contain("Captured Ada: 7.").And.NotContain("{\"name\"");
        capture.StandardError.Should().Contain("Capture (UTF-16 code units): stdout 14 | stderr 18")
            .And.NotContain("private diagnostic");

        var timeout = await consumer.RunAsync("process-timeout", "--plain");
        timeout.ExitCode.Should().Be(1);
        timeout.StandardOutput.Should().Contain("Child output before deadline.");
        timeout.StandardError.Should().Contain("Process deadline reached | dotnet")
            .And.Contain("Process timed out | dotnet").And.Contain("Timeout: 2s | elapsed:")
            .And.NotContain("ProcessTimeoutException:");
        var recovered = await consumer.RunAsync("process-timeout-recover", "--quiet");
        recovered.ExitCode.Should().Be(0);
        recovered.StandardOutput.Should().Contain("Recovered from dotnet timeout: 2s.").And.Contain("Fallback: True.");
        recovered.StandardError.Should().BeEmpty();
    }
}
