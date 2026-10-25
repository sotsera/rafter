using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Inputs;

public sealed class InputDiagnosticTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Independent_conversion_errors_are_reported_in_either_declaration_order(int types, bool reverse)
    {
        using var error = new StringWriter();
        var command = CreateCommand(error);
        foreach (var name in reverse ? new[] { "second", "first" } : ["first", "second"])
        {
            if (types == 0 || types == 2 && name == "first")
            {
                command.Option<TimeSpan>(name, "Duration.").FromEnvironment("MUST_NOT_READ");
            }
            else
            {
                command.Option<Choice>(name, "Choice.").FromEnvironment("MUST_NOT_READ");
            }
        }
        command.Validate("Must not validate invalid input.", _ => throw new Exception("validator ran"));
        var code = await command.RunAsync(["--first=private-invalid", "--second=private-invalid", "--plain"],
            _ => throw new Exception("body ran"));
        code.Should().Be(2);
        error.ToString().Should().Contain("--first requires").And.Contain("--second requires")
            .And.NotContain("private-invalid").And.NotContain("validator ran").And.NotContain("body ran")
            .And.NotContain("environment lookup ran");
    }

    [Theory]
    [InlineData("--count=private-invalid")]
    [InlineData("--missing")]
    [InlineData("--repeat=1", "--repeat=2")]
    [InlineData("--unknown")]
    public async Task Structural_errors_do_not_hide_an_independently_convertible_duration(params string[] invalid)
    {
        using var error = new StringWriter();
        var command = CreateCommand(error);
        command.Option<int>("count", "Count.");
        command.Option<TimeSpan>("missing", "Missing value.");
        command.Option<int>("repeat", "Repeated option.");
        command.Option<TimeSpan>("duration", "Duration.");
        var code = await command.RunAsync([.. invalid, "--duration=private-invalid", "--plain"],
            _ => throw new Exception("body ran"));
        code.Should().Be(2);
        error.ToString().Should().Contain("--duration requires").And.NotContain("private-invalid")
            .And.NotContain("body ran").And.NotContain("environment lookup ran");
        var structuralDiagnostic = invalid[0] == "--unknown" ? "Unexpected input" : invalid[0].Split('=')[0];
        error.ToString().Should().Contain(structuralDiagnostic);
    }

    private static Command CreateCommand(StringWriter error)
    {
        var command = new Command("Input diagnostics.", () => new InvocationHost(Directory.GetCurrentDirectory(), TextWriter.Null,
            error, true, true, false, 160, _ => throw new Exception("subscription ran"))
        {
            ReadEnvironment = _ => throw new Exception("environment lookup ran")
        });
        command.Option<string>("environment", "Unrelated fallback.").FromEnvironment("MUST_NOT_READ");
        return command;
    }

    private enum Choice
    {
        First,
        Second
    }
}
