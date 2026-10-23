using AwesomeAssertions;
using Xunit;

namespace Rafter.WindowsTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class ProcessConsumerTests
{
    [WindowsFact]
    public async Task Capture_example_parses_JSON_and_exposes_echo_order_rejection_and_overflow()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!,
            "examples/process-capture.cs");
        await consumer.BuildAsync();
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("--scenario <value>").And.Contain("--limit <int>");
        help.StandardError.Should().BeEmpty();
        var success = await consumer.RunAsync("--quiet");
        success.ExitCode.Should().Be(0, success.StandardError);
        success.StandardOutput.Should().Contain("Captured greeting for Ada: Hello, Ada!").And.NotContain("{\"name\"");
        success.StandardError.Should().BeEmpty();
        var echoed = await consumer.RunAsync("--quiet", "--echo", "--show-order");
        echoed.ExitCode.Should().Be(0);
        echoed.StandardOutput.Should().Contain("{\"name\":\"Ada\"").And.Contain("stdout:").And.Contain("stderr:");
        echoed.StandardError.Should().Contain("Child diagnostic.");
        var rejected = await consumer.RunAsync("--quiet", "--scenario", "Failure");
        rejected.ExitCode.Should().Be(1);
        rejected.StandardOutput.Should().BeEmpty();
        rejected.StandardError.Should().Contain("Process failed | dotnet").And.Contain("Exit code: 7")
            .And.NotContain("could not produce");
        var overflow = await consumer.RunAsync("--quiet", "--scenario", "Overflow");
        overflow.ExitCode.Should().Be(1);
        overflow.StandardOutput.Should().BeEmpty();
        overflow.StandardError.Should().Contain("Process capture limit exceeded | dotnet")
            .And.Contain("Partial capture (UTF-16 code units): stdout 1024 | stderr 0")
            .And.NotContain(new string('x', 20)).And.NotContain("Exception:");
        var invalid = await consumer.RunAsync("--limit", "0", "--quiet");
        invalid.ExitCode.Should().Be(2);
        invalid.StandardError.Should().Contain("Must be greater than zero").And.NotContain("Process |");
    }

    [WindowsFact]
    public async Task Timeout_example_validates_inputs_and_runs_with_successful_and_expired_deadlines()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!,
            "examples/process-timeout.cs");
        consumer.EnvironmentOverrides["RAFTER_PROCESS_TIMEOUT"] = null;
        await consumer.BuildAsync();
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("--timeout <duration>").And.Contain("Default: 10s.")
            .And.Contain("Environment: RAFTER_PROCESS_TIMEOUT.");
        var invalid = await consumer.RunAsync("--project", "unused.csproj", "--timeout", "0s", "--quiet");
        invalid.ExitCode.Should().Be(2);
        invalid.StandardError.Should().Contain("Must be greater than zero.").And.NotContain("Process |");
        var project = "tests/testassets/workflows/src/BuildSample.csproj";
        var success = await consumer.RunAsync("--project", project, "--timeout", "1m", "--quiet");
        success.ExitCode.Should().Be(0, success.StandardError);
        success.StandardOutput.Should().Contain("Build completed before its process deadline.");
        // One tick expires before a build can finish, without assuming the machine's build duration.
        var expired = await consumer.RunAsync("--project", project, "--timeout", "0.0001ms", "--quiet");
        expired.ExitCode.Should().Be(1);
        expired.StandardOutput.Should().NotContain("Build completed before its process deadline.");
        expired.StandardError.Should().Contain("Process timed out | dotnet").And.Contain("Timeout: 0.0001ms")
            .And.NotContain("deadline reached").And.NotContain("Exception:");
    }

    [WindowsFact]
    public async Task Actual_process_example_runs_from_an_isolated_directory()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/processes.cs");
        await consumer.BuildAsync();
        var run = await consumer.RunAsync("--quiet");
        run.ExitCode.Should().Be(0);
        run.StandardOutput.Should().Contain("10.0.").And.EndWith("SDK inspection completed." + Environment.NewLine);
        run.StandardError.Should().BeEmpty();
        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("Display the installed .NET SDK version.")
            .And.NotContain("SDK inspection completed.");
        help.StandardError.Should().BeEmpty();
    }

    [WindowsFact]
    public async Task Working_directory_example_builds_the_child_project()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!,
            "examples/process-working-directory.cs");
        await consumer.BuildAsync();
        var run = await consumer.RunFromAsync("tests/testassets/workflows", "--quiet");
        run.ExitCode.Should().Be(0, run.StandardError);
        run.StandardOutput.Should().Contain("Build succeeded.").And.Contain("Build completed.");
        File.Exists(Path.Combine(consumer.WorkingDirectory,
            "tests/testassets/workflows/src/bin/Debug/net10.0/BuildSample.dll")).Should().BeTrue();
    }

    [WindowsFact]
    public async Task Failed_build_example_returns_the_unsuccessful_result_and_preserves_invocation_failure()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!,
            "examples/process-failure.cs");
        await consumer.BuildAsync();
        var success = await consumer.RunAsync("--project", "tests/testassets/workflows/src/BuildSample.csproj", "--quiet");
        success.ExitCode.Should().Be(0, success.StandardError);
        success.StandardOutput.Should().Contain("Build completed.");
        await File.AppendAllTextAsync(Path.Combine(consumer.WorkingDirectory, "tests/testassets/workflows/src/Greeting.cs"),
            "\n#error Intentional fixture build failure\n", TestContext.Current.CancellationToken);
        var failure = await consumer.RunAsync("--project", "tests/testassets/workflows/src/BuildSample.csproj", "--quiet");
        failure.ExitCode.Should().Be(1);
        failure.StandardOutput.Should().Contain("Build exited with code 1.").And.NotContain("Build completed.");
        failure.StandardError.Should().Contain("Action: Fail").And.Contain("[Failed]");
    }

    [WindowsFact]
    public async Task Dependency_example_builds_and_tests_and_stops_at_the_failing_target()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!,
            "examples/dependencies.cs");
        await consumer.BuildAsync();
        var success = await consumer.RunAsync("--project", "tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj");
        success.ExitCode.Should().Be(0, success.StandardOutput + success.StandardError);
        success.StandardOutput.Should().Contain("succeeded: 1");
        success.StandardError.Should().Contain("[build]").And.Contain("[test] <-- (build)")
            .And.Contain("2 target bodies completed");

        var testPath = Path.Combine(consumer.WorkingDirectory, "tests/testassets/workflows/sample-tests/GreetingTests.cs");
        var source = await File.ReadAllTextAsync(testPath, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(testPath, source.Replace("Hello from the build fixture.", "Wrong expectation."),
            TestContext.Current.CancellationToken);
        var failedTest = await consumer.RunAsync("--project", "tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj");
        failedTest.ExitCode.Should().Be(1);
        failedTest.StandardOutput.Should().Contain("failed: 1");
        failedTest.StandardError.Should().Contain("stopped in test");

        await File.AppendAllTextAsync(Path.Combine(consumer.WorkingDirectory, "tests/testassets/workflows/src/Greeting.cs"),
            "\n#error Intentional fixture build failure\n", TestContext.Current.CancellationToken);
        var failedBuild = await consumer.RunAsync("--project", "tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj");
        failedBuild.ExitCode.Should().Be(1);
        failedBuild.StandardError.Should().Contain("stopped in build").And.NotContain("[test]");
    }
}
