using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Rafter.WindowsTests.Processes;

[Collection("Native process handles")]
public sealed class SensitiveProcessTests
{
    [WindowsTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Raw_child_inputs_capture_and_echo_remain_usable_while_script_presentation_is_masked(
        bool echo, bool environment)
    {
        using var host = new ToolTestHost(Directory.GetCurrentDirectory());
        var token = host.Command.Option<string>("token", "Token.").Sensitive().Default("child-secret");
        string? captured = null;
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            var raw = context.GetValue(token);
            var arguments = environment
                ? WindowsProcessTests.ProbeArguments("environment", "RAFTER_REDACTION_CHILD")
                : WindowsProcessTests.ProbeArguments("echo", raw);
            var result = await context.Executable("dotnet").Arguments(arguments)
                .Environment("RAFTER_REDACTION_CHILD", raw).Timeout(TimeSpan.FromSeconds(30))
                .RunAndCaptureAsync(4096, echo);
            captured = result.StandardOutput.Text;
            var actual = JsonSerializer.Deserialize<string[]>(captured)!;
            actual[^1].Should().Be(raw);
            context.WriteLine(captured);
        })).Should().Be(0, host.Error.ToString());
        captured.Should().Contain("child-secret");
        var masked = captured!.Replace("child-secret", "[REDACTED]", StringComparison.Ordinal);
        host.Output.ToString().Should().Be((echo ? captured : "") + masked + Environment.NewLine);
        host.Error.ToString().Should().BeEmpty();
    }
}
