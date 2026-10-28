using System.Text.Json;
using AwesomeAssertions;
using Sotsera.Rafter;
using Xunit;

namespace Rafter.WindowsTests.Processes;

[Collection("Native process handles")]
public sealed class SdkEnvironmentTests
{
    [WindowsFact]
    public async Task Sdk_policy_preserves_unrelated_host_and_tool_settings()
    {
        const string name = "DOTNET_HOST_PATH";
        var original = Environment.GetEnvironmentVariable(name);
        using var host = new ToolTestHost(Directory.GetCurrentDirectory());
        Environment.SetEnvironmentVariable(name, "preserved-host-path");
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                var result = await context.Dotnet().UseWorkingDirectorySdk()
                    .Arguments(WindowsProcessTests.ProbeArguments("environment-state", name)).RunAndCaptureAsync(4096);
                using var json = JsonDocument.Parse(result.StandardOutput.Text);
                json.RootElement.GetProperty("Value").GetString().Should().Be("preserved-host-path");
                Environment.GetEnvironmentVariable(name).Should().Be("preserved-host-path");
            })).Should().Be(0, host.Error.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }

    [WindowsTheory]
    [InlineData("MSBuildSDKsPath")]
    [InlineData("MSBUILD_EXE_PATH")]
    [InlineData("MSBuildExtensionsPath")]
    [InlineData("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR")]
    [InlineData("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER")]
    [InlineData("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR")]
    public async Task Sdk_policy_removes_inherited_overrides_only_for_opted_in_children(string name)
    {
        var original = Environment.GetEnvironmentVariable(name);
        using var host = new ToolTestHost(Directory.GetCurrentDirectory());
        Environment.SetEnvironmentVariable(name, "contaminated");
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                var inherited = context.Dotnet();
                var selected = inherited.UseWorkingDirectorySdk();
                var arguments = WindowsProcessTests.ProbeArguments("environment-state", name);
                var raw = await inherited.Arguments(arguments).RunAndCaptureAsync(4096);
                var isolated = await selected.Arguments(arguments).RunAndCaptureAsync(4096);
                var overridden = await selected.Arguments(arguments).Environment(name, "explicit").RunAndCaptureAsync(4096);
                using var rawJson = JsonDocument.Parse(raw.StandardOutput.Text);
                using var isolatedJson = JsonDocument.Parse(isolated.StandardOutput.Text);
                using var overriddenJson = JsonDocument.Parse(overridden.StandardOutput.Text);
                rawJson.RootElement.GetProperty("Value").GetString().Should().Be("contaminated");
                isolatedJson.RootElement.GetProperty("Present").GetBoolean().Should().BeFalse();
                overriddenJson.RootElement.GetProperty("Value").GetString().Should().Be("explicit");
                Environment.GetEnvironmentVariable(name).Should().Be("contaminated");
            })).Should().Be(0, host.Error.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }

    [WindowsFact]
    [Trait("LongRunning", "true")]
    public async Task Sdk_policy_restores_actual_metadata_and_resource_build_without_requiring_a_second_sdk()
    {
        var temporary = Directory.CreateTempSubdirectory("rafter-sdk-environment-");
        var root = temporary.CreateSubdirectory("selected root").FullName;
        var original = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
        await File.WriteAllTextAsync(Path.Combine(root, "global.json"), """
            { "sdk": { "version": "10.0.402", "rollForward": "latestPatch", "allowPrerelease": false } }
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "Sample.cs"), "public class Sample { }");
        await File.WriteAllTextAsync(Path.Combine(root, "Words.resx"),
            "<root><data name=\"Greeting\" xml:space=\"preserve\"><value>Hello</value></data></root>");
        using var host = new ToolTestHost(temporary.FullName);
        var invalidSdk = Path.Combine(temporary.FullName, "missing SDK");
        Environment.SetEnvironmentVariable("MSBuildSDKsPath", invalidSdk);
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                var inherited = context.Dotnet().WorkingDirectory("selected root").Timeout(TimeSpan.FromMinutes(2));
                var failed = await inherited.Build("Sample.csproj").RunAndCaptureAsync(16384,
                    onFailure: _ => ProcessFailureAction.Continue);
                failed.Succeeded.Should().BeFalse();
                (failed.StandardOutput.Text + failed.StandardError.Text).Should().Contain("MSB4236");
                var selected = inherited.UseWorkingDirectorySdk();
                var metadata = await selected.Arguments("msbuild", "Sample.csproj")
                    .Arguments("-nologo", "-getProperty:NETCoreSdkVersion,MSBuildSDKsPath")
                    .RunAndCaptureAsync(16384);
                using var json = JsonDocument.Parse(metadata.StandardOutput.Text);
                json.RootElement.GetProperty("Properties").GetProperty("NETCoreSdkVersion").GetString()
                    .Should().StartWith("10.0.");
                await selected.Build("Sample.csproj").Configuration("Release").DisableBuildServers().MaxCpuCount(1).RunAsync();
                File.Exists(Path.Combine(root, "bin", "Release", "net10.0", "Sample.dll")).Should().BeTrue();
                Environment.GetEnvironmentVariable("MSBuildSDKsPath").Should().Be(invalidSdk);
            })).Should().Be(0, host.Error.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", original);
            temporary.Delete(true);
        }
    }
}
