using System.Text.Json;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.WindowsTests.Processes;

[Collection("Native process handles")]
public sealed class RootProcessTests
{
    [WindowsTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Selected_root_controls_child_directories_without_rewriting_arguments(bool optionBound, bool childOverride)
    {
        var temporary = Directory.CreateTempSubdirectory("rafter-root-process-");
        var selected = temporary.CreateSubdirectory("selected").FullName;
        var nested = Directory.CreateDirectory(Path.Combine(selected, "nested")).FullName;
        using var output = new StringWriter();
        using var error = new StringWriter();
        var command = new Command("Selected process root.", () => new InvocationHost(temporary.FullName, output, error,
            true, true, false, 100, _ => new Subscription()));
        if (optionBound)
        {
            command.RootDirectory(command.Option<string>("root", "Root.").Default("selected"));
        }
        else
        {
            command.RootDirectory("selected");
        }
        var original = Directory.GetCurrentDirectory();
        try
        {
            (await command.RunAsync(["--quiet"], async context =>
            {
                var probe = context.Executable("dotnet").Arguments(WindowsProcessTests.ProbeArguments("environment", "RAFTER_MISSING_ROOT_VAR"));
                if (childOverride)
                {
                    probe.WorkingDirectory("nested");
                }
                var captured = await probe.RunAndCaptureAsync(4096);
                JsonSerializer.Deserialize<string?[]>(captured.StandardOutput.Text)![0]
                    .Should().Be(childOverride ? nested : selected);
                var arguments = await context.Executable("dotnet")
                    .Arguments(WindowsProcessTests.ProbeArguments("echo", "relative/input.txt"))
                    .RunAndCaptureAsync(4096);
                JsonSerializer.Deserialize<string[]>(arguments.StandardOutput.Text).Should().Equal("relative/input.txt");
            })).Should().Be(0, error.ToString());
            Directory.GetCurrentDirectory().Should().Be(original);
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    private sealed class Subscription : IDisposable
    {
        public void Dispose() { }
    }
}
