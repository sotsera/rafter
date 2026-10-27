using System.Text.Json;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.WindowsTests.Processes;

[Collection("Native process handles")]
public sealed class ExecutableTests
{
    [WindowsFact]
    public async Task Reusable_executable_creates_independent_argument_vectors_with_defensive_copies()
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            var executable = context.Executable("dotnet");
            string[] arguments = ["", "a b", "\"quoted\"", "a&b|c", "héllo 世界"];
            var first = executable.Arguments(WindowsProcessTests.ProbeArguments("echo"))
                .Arguments(arguments).Arguments("--property", "value");
            arguments[1] = "mutated";
            var second = executable.Arguments(WindowsProcessTests.ProbeArguments("echo", "second"));
            var results = await Task.WhenAll(first.RunAndCaptureAsync(4096), second.RunAndCaptureAsync(4096));
            JsonSerializer.Deserialize<string[]>(results[0].StandardOutput.Text)
                .Should().Equal("", "a b", "\"quoted\"", "a&b|c", "héllo 世界", "--property", "value");
            JsonSerializer.Deserialize<string[]>(results[1].StandardOutput.Text).Should().Equal("second");
        })).Should().Be(0, host.Error.ToString());
    }

    [WindowsFact]
    public async Task Environment_removal_empty_values_and_child_overrides_do_not_mutate_defaults_or_parent()
    {
        var name = "RAFTER_EXECUTABLE_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(name, "parent");
        using var host = new Host();
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                var original = context.Executable("dotnet");
                var removed = original.RemoveEnvironment(name);
                var configured = original.Environment(name, "default");
                var arguments = WindowsProcessTests.ProbeArguments("environment-state", name);
                await CheckAsync(original.Arguments(arguments), "parent", true);
                await CheckAsync(removed.Arguments(arguments), null, false);
                await CheckAsync(removed.Arguments(arguments).Environment(name, ""), "", true);
                await CheckAsync(configured.Arguments(arguments).RemoveEnvironment(name), null, false);
                await CheckAsync(configured.Arguments(arguments).Environment(name, "child"), "child", true);
                await CheckAsync(configured.Arguments(arguments), "default", true);
                Environment.GetEnvironmentVariable(name).Should().Be("parent");
            })).Should().Be(0, host.Error.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [WindowsFact]
    public async Task Executable_directory_default_and_child_override_resolve_against_invocation_root()
    {
        var temporary = Directory.CreateTempSubdirectory("rafter-executable-");
        var defaultDirectory = temporary.CreateSubdirectory("default").FullName;
        var childDirectory = temporary.CreateSubdirectory("child").FullName;
        using var host = new Host(temporary.FullName);
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                var executable = context.Executable("dotnet").WorkingDirectory("default");
                var arguments = WindowsProcessTests.ProbeArguments("environment-state", "PATH");
                var first = await executable.Arguments(arguments).WorkingDirectory("child").RunAndCaptureAsync(16384);
                var second = await executable.Arguments(arguments).RunAndCaptureAsync(16384);
                using var firstJson = JsonDocument.Parse(first.StandardOutput.Text);
                using var secondJson = JsonDocument.Parse(second.StandardOutput.Text);
                firstJson.RootElement.GetProperty("Directory").GetString().Should().Be(childDirectory);
                secondJson.RootElement.GetProperty("Directory").GetString().Should().Be(defaultDirectory);
            })).Should().Be(0, host.Error.ToString());
        }
        finally
        {
            temporary.Delete(true);
        }
    }

    [WindowsFact]
    public async Task Zero_argument_runs_create_fresh_commands_and_stale_executable_cannot_rebind_to_next_body()
    {
        using var host = new Host();
        Executable? stale = null;
        var first = host.Command.Target("first", "First.").Run(async context =>
        {
            stale = context.Executable(ProbeExecutable());
            var captured = await stale.RunAndCaptureAsync(1024);
            captured.StandardOutput.Text.Should().Be("[]");
            (await stale.RunAsync()).Succeeded.Should().BeTrue();
        });
        var second = host.Command.Target("second", "Second.").DependsOn(first).Run(async _ =>
        {
            Func<Task> launch = async () => { await stale!.RunAsync(); };
            await launch.Should().ThrowAsync<InvalidOperationException>().WithMessage("*after Target 'first' has finished*");
        });
        (await host.Command.RunAsync(["--quiet"], second)).Should().Be(0, host.Error.ToString());
    }

    [WindowsTheory]
    [InlineData("build")]
    [InlineData("publish")]
    [InlineData("pack")]
    public async Task Typed_commands_preserve_option_pairs_raw_arguments_and_command_identity(string verb)
    {
        using var host = new Host();
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            var dotnet = new DotnetExecutable(context.Executable(ProbeExecutable()));
            const string path = "project with spaces/app.csproj";
            Task<CapturedProcessResult> operation = verb switch
            {
                "build" => dotnet.Build(path).Configuration("Custom").NoRestore().DisableBuildServers().MaxCpuCount(1)
                    .Arguments("-p:Name=a b").RunAndCaptureAsync(4096),
                "publish" => dotnet.Publish(path).Configuration("Custom").NoRestore().NoBuild().Output("output directory")
                    .Arguments("-p:Name=a b").RunAndCaptureAsync(4096),
                _ => dotnet.Pack(path).Configuration("Custom").NoRestore().NoBuild().Output("output directory")
                    .Arguments("-p:Name=a b").RunAndCaptureAsync(4096)
            };
            var result = await operation;
            string[] options = verb == "build"
                ? ["--disable-build-servers", "-m:1"] : ["--no-build", "--output", "output directory"];
            JsonSerializer.Deserialize<string[]>(result.StandardOutput.Text).Should().Equal(
                [verb, path, "--configuration", "Custom", "--no-restore", .. options, "-p:Name=a b"]);
            var next = await dotnet.Build().RunAndCaptureAsync(1024);
            JsonSerializer.Deserialize<string[]>(next.StandardOutput.Text).Should().Equal("build");
        })).Should().Be(0, host.Error.ToString());
    }

    [WindowsFact]
    [Trait("LongRunning", "true")]
    public async Task Typed_dotnet_build_publish_and_pack_work_with_a_real_project()
    {
        var temporary = Directory.CreateTempSubdirectory("rafter-typed-dotnet-");
        await File.WriteAllTextAsync(Path.Combine(temporary.FullName, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <PackageId>Rafter.Typed.Sample</PackageId>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(temporary.FullName, "Sample.cs"), "public class Sample { }");
        using var host = new Host(temporary.FullName);
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                var dotnet = context.Dotnet().Timeout(TimeSpan.FromMinutes(2))
                    .Environment("DOTNET_CLI_USE_MSBUILD_SERVER", "0");
                await dotnet.Build("Sample.csproj").Configuration("Custom").DisableBuildServers().MaxCpuCount(1).RunAsync();
                await dotnet.Publish("Sample.csproj").Configuration("Custom").NoBuild().Output("published").RunAsync();
                await dotnet.Pack("Sample.csproj").Configuration("Custom").NoBuild().Output("packages").RunAsync();
                File.Exists(context.File("published", "Sample.dll").FullPath).Should().BeTrue();
                Directory.GetFiles(context.Directory("packages").FullPath, "Rafter.Typed.Sample.*.nupkg").Should().ContainSingle();
            })).Should().Be(0, host.Error.ToString());
        }
        finally
        {
            temporary.Delete(true);
        }
    }

    private static string ProbeExecutable() => Path.Combine(AppContext.BaseDirectory, "ProcessProbe.exe");

    private static async Task CheckAsync(ProcessCommand command, string? value, bool present)
    {
        var captured = await command.RunAndCaptureAsync(4096);
        using var json = JsonDocument.Parse(captured.StandardOutput.Text);
        json.RootElement.GetProperty("Value").GetString().Should().Be(value);
        json.RootElement.GetProperty("Present").GetBoolean().Should().Be(present);
    }

    private sealed class Host : IDisposable
    {
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public Host(string? root = null)
        {
            Command = new("Executable API.", () => new InvocationHost(root ?? Directory.GetCurrentDirectory(),
                Output, Error, true, true, false, 100, _ => new Subscription()));
        }

        public void Dispose()
        {
            TestContext.Current.TestOutputHelper?.WriteLine(Output + "\n" + Error);
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription : IDisposable
    {
        public void Dispose() { }
    }
}
