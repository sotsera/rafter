using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Rafter.WindowsTests.Consumers;

[Trait("LongRunning", "true")]
// Full SDK workflows must not overlap short-deadline native consumer probes.
[Collection("Native process handles")]
public sealed class RepositoryWorkflowTests
{
    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_builds_tests_and_optionally_packs_from_an_explicit_root(bool explicitRoot)
    {
        using var consumer = CreateConsumer();
        await consumer.BuildAsync();

        var help = await consumer.RunAsync("--help");
        help.ExitCode.Should().Be(0);
        help.StandardOutput.Should().Contain("--root").And.Contain("--pack").And.Contain("--full-tests");
        Directory.Exists(EvidenceRoot(consumer)).Should().BeFalse();

        var invalid = await consumer.RunAsync("--timeout", "0s", "--quiet");
        invalid.ExitCode.Should().Be(2);
        Directory.Exists(EvidenceRoot(consumer)).Should().BeFalse();

        var result = explicitRoot
            ? await consumer.RunFromAsync("tests/testassets/workflows", "--root", "../../..",
                "--configuration", "Debug", "--full-tests", "--pack", "--quiet", "--plain")
            : await consumer.RunAsync("--quiet", "--plain");
        result.ExitCode.Should().Be(0, result.StandardOutput + result.StandardError);
        result.StandardOutput.Should().Contain("succeeded: 1").And.Contain("Package: Sotsera.Rafter");

        using var manifest = ReadManifest(consumer);
        var record = manifest.RootElement;
        record.GetProperty("Succeeded").GetBoolean().Should().BeTrue();
        record.GetProperty("Repository").GetString().Should().Be(consumer.WorkingDirectory);
        record.GetProperty("Configuration").GetString().Should().Be(explicitRoot ? "Debug" : "Release");
        record.GetProperty("FullTests").GetBoolean().Should().Be(explicitRoot);
        record.GetProperty("PackRequested").GetBoolean().Should().Be(explicitRoot);
        Stages(record).Should().Equal(explicitRoot ? ["inspect", "build", "test", "pack"] : ["inspect", "build", "test"]);

        var evidence = Directory.GetDirectories(EvidenceRoot(consumer)).Should().ContainSingle().Subject;
        File.Exists(Path.Combine(evidence, "build.log")).Should().BeTrue();
        File.Exists(Path.Combine(evidence, "package-properties.json")).Should().BeTrue();
        Directory.GetFiles(Path.Combine(evidence, "tests"), "*.trx", SearchOption.AllDirectories)
            .Should().ContainSingle();
        if (explicitRoot)
        {
            var packages = record.GetProperty("PackageDirectory").GetString()!;
            Directory.GetFiles(packages, "Sotsera.Rafter.*.nupkg").Should().ContainSingle();
        }
        else
        {
            record.GetProperty("PackageDirectory").ValueKind.Should().Be(JsonValueKind.Null);
            result.StandardOutput.Should().Contain("Packaging skipped");
        }
        result.StandardOutput.Should().NotContain("\x1b");
        result.StandardError.Should().BeEmpty();
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_stops_after_a_failed_build_or_test_and_retains_evidence(bool failTest)
    {
        using var consumer = CreateConsumer();
        await consumer.BuildAsync();
        if (failTest)
        {
            var path = Path.Combine(consumer.WorkingDirectory, "tests/testassets/workflows/sample-tests/GreetingTests.cs");
            var source = await File.ReadAllTextAsync(path);
            await File.WriteAllTextAsync(path, source.Replace("Hello from the build fixture.", "Wrong expectation."));
        }
        else
        {
            await File.AppendAllTextAsync(Path.Combine(consumer.WorkingDirectory, "tests/testassets/workflows/src/Greeting.cs"),
                "\n#error Intentional workflow build failure\n");
        }

        var result = await consumer.RunAsync("--pack", "--quiet", "--plain");
        result.ExitCode.Should().Be(1);
        result.StandardError.Should().Contain("Process failed | dotnet");
        result.StandardOutput.Should().NotContain("Local packages:");
        if (failTest)
        {
            result.StandardOutput.Should().Contain("failed: 1");
        }
        else
        {
            result.StandardOutput.Should().Contain("Intentional workflow build failure");
        }

        using var manifest = ReadManifest(consumer);
        var record = manifest.RootElement;
        record.GetProperty("Succeeded").GetBoolean().Should().BeFalse();
        record.GetProperty("PackageDirectory").ValueKind.Should().Be(JsonValueKind.Null);
        Stages(record).Should().Equal(failTest ? ["inspect", "build"] : ["inspect"]);
        Directory.GetFiles(EvidenceRoot(consumer), "*.nupkg", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [WindowsFact]
    public async Task Workflow_timeout_retains_its_manifest_and_prevents_later_work()
    {
        using var consumer = CreateConsumer();
        await consumer.BuildAsync();
        var result = await consumer.RunAsync("--pack", "--timeout", "0.0001ms", "--quiet", "--plain");
        result.ExitCode.Should().Be(1);
        result.StandardError.Should().Contain("Process timed out | dotnet");
        using var manifest = ReadManifest(consumer);
        manifest.RootElement.GetProperty("Succeeded").GetBoolean().Should().BeFalse();
        Stages(manifest.RootElement).Should().BeEmpty();
        Directory.GetFiles(EvidenceRoot(consumer), "*.nupkg", SearchOption.AllDirectories).Should().BeEmpty();
    }

    private static IsolatedConsumer CreateConsumer()
    {
        var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "examples/repository-workflow.cs");
        // A small real solution avoids recursively running the outer repository's workflow tests.
        File.WriteAllText(Path.Combine(consumer.WorkingDirectory, "Rafter.slnx"), """
            <Solution>
              <Project Path="src/Sotsera.Rafter/Sotsera.Rafter.csproj" />
              <Project Path="tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj" />
            </Solution>
            """);
        return consumer;
    }

    private static string EvidenceRoot(IsolatedConsumer consumer)
        => Path.Combine(consumer.WorkingDirectory, "artifacts", "evidence", "workflows", "repository");

    private static JsonDocument ReadManifest(IsolatedConsumer consumer)
    {
        var path = Directory.GetFiles(EvidenceRoot(consumer), "run.json", SearchOption.AllDirectories)
            .Should().ContainSingle().Subject;
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static IEnumerable<string?> Stages(JsonElement record)
        => record.GetProperty("CompletedStages").EnumerateArray().Select(stage => stage.GetString());
}
