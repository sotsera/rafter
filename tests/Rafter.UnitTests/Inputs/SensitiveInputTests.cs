using System.Text.RegularExpressions;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Spectre.Console;
using Xunit;

namespace Rafter.UnitTests.Inputs;

public sealed class SensitiveInputTests
{
    [Theory]
    [InlineData("--token")]
    [InlineData("-t")]
    public async Task Repeated_sensitive_arguments_still_mask_all_recognized_values_in_diagnostics(string repeated)
    {
        using var host = new InputHost();
        host.Command.Option<string>("token", "Token.").Aliases("t").Sensitive();
        host.Command.Option<int>("first-secret", "First diagnostic.");
        host.Command.Option<int>("second-secret", "Second diagnostic.");
        (await host.Command.RunAsync(["--token", "first-secret", repeated, "second-secret",
            "--first-secret", "invalid", "--second-secret", "invalid"],
            _ => throw new Exception("must not enter work"))).Should().Be(2);
        host.Error.ToString().Should().Contain("may only be supplied once").And.Contain("[REDACTED]")
            .And.NotContain("first-secret").And.NotContain("second-secret");
    }

    [Theory]
    [InlineData("default-before")]
    [InlineData("default-after")]
    [InlineData("required")]
    [InlineData("optional")]
    public async Task Fluent_orders_preserve_raw_values_and_mask_literal_output(string kind)
    {
        using var host = new InputHost();
        var option = host.Command.Option<string>("token", "Token.");
        ValueOption<string>? value = kind switch
        {
            "default-before" => option.Sensitive().Default("raw-token"),
            "default-after" => option.Default("raw-token").Sensitive(),
            "required" => option.Required().Sensitive(),
            _ => null
        };
        option.Sensitive().Sensitive().Should().BeSameAs(option);
        (await host.Command.RunAsync(["--quiet", "--token", "raw-token"], context =>
        {
            var raw = value is null ? context.GetValue(option) : context.GetValue(value);
            raw.Should().Be("raw-token");
            context.WriteLine($"  [{raw}]\n{raw}  ");
        })).Should().Be(0);
        host.Output.ToString().Should().Be("  [[REDACTED]]\n[REDACTED]  " + Environment.NewLine);
        host.Error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("int")]
    [InlineData("bool")]
    [InlineData("enum")]
    [InlineData("duration")]
    public async Task Nonstring_sensitivity_is_invalid_even_for_help(string kind)
    {
        using var host = new InputHost();
        switch (kind)
        {
            case "int":
                host.Command.Option<int>("value", "Value.").Sensitive();
                break;
            case "bool":
                host.Command.Option<bool>("value", "Value.").Default(false).Sensitive();
                break;
            case "enum":
                host.Command.Option<DayOfWeek>("value", "Value.").Required().Sensitive();
                break;
            default:
                host.Command.Option<TimeSpan>("value", "Value.").Sensitive();
                break;
        }
        (await host.Command.RunAsync(["--help"], _ => throw new Exception("must not run"))).Should().Be(2);
        host.Error.ToString().Should().Contain("Sensitive inputs must use string values");
        host.Output.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(48, false)]
    [InlineData(100, false)]
    [InlineData(48, true)]
    [InlineData(100, true)]
    public async Task Help_masks_defaults_and_styled_boundaries_without_lookup_or_callbacks(int width, bool rich)
    {
        using var host = new InputHost(width, rich);
        var entered = false;
        host.Command.Option<string>("token", "Includes default-secret.")
            .Sensitive().Default("default-secret").FromEnvironment("TOKEN")
            .Validate("Never evaluated.", _ => throw new Exception("must not validate"));
        host.Command.Option<string>("required", "Required.").Required().Sensitive();
        host.Command.Option<string>("optional", "Optional.").Sensitive();
        // This value crosses separate styled runs in the input heading.
        host.Command.Option<string>("boundary", "Boundary.").Sensitive().Default("token <string>");
        host.Command.State<object>(() => throw new Exception("must not initialize"));
        host.Command.Cleanup("Never entered.", _ => entered = true);
        (await host.Command.RunAsync(["--help", "--token", "cli-secret"], _ => entered = true)).Should().Be(0);
        var output = Strip(host.Output.ToString());
        output.Should().Contain("Sensitive.").And.Contain("[REDACTED]")
            .And.NotContain("default-secret").And.NotContain("cli-secret").And.NotContain("token <string>");
        host.Lookups.Should().BeEmpty();
        host.Error.ToString().Should().BeEmpty();
        entered.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("R")]
    [InlineData("REDACTED")]
    [InlineData("[REDACTED]")]
    public async Task Sensitive_defaults_use_one_marker_including_empty_and_marker_overlaps(string value)
    {
        using var host = new InputHost();
        host.Command.Option<string>("token", "Token.").Sensitive().Default(value);
        (await host.Command.RunAsync(["--help"], _ => { })).Should().Be(0);
        host.Output.ToString().Should().Contain("Default: [REDACTED].")
            .And.NotContain("[[REDACTED]]").And.NotContain("[REDACTED]ACTED");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sources_preserve_precedence_and_register_only_considered_environment(bool cli)
    {
        using var host = new InputHost();
        host.Environment["TOKEN"] = "environment-secret";
        var token = host.Command.Option<string>("token", "Token.").FromEnvironment("TOKEN")
            .Sensitive().Default("default-secret");
        var arguments = cli ? new[] { "--verbose", "--token=cli-secret" } : ["--verbose"];
        (await host.Command.RunAsync(arguments, context =>
        {
            context.GetValue(token).Should().Be(cli ? "cli-secret" : "environment-secret");
            context.WriteLine("default-secret cli-secret environment-secret");
        })).Should().Be(0);
        host.Output.ToString().Should().Be(cli
            ? "[REDACTED] [REDACTED] environment-secret" + Environment.NewLine
            : "[REDACTED] cli-secret [REDACTED]" + Environment.NewLine);
        host.Lookups.Should().HaveCount(cli ? 0 : 1);
        host.Error.ToString().Should().Contain(cli ? "command line" : "environment TOKEN")
            .And.NotContain("default-secret").And.NotContain("environment-secret");
    }

    [Fact]
    public async Task All_selected_inputs_are_registered_before_the_first_validation_callback()
    {
        using var host = new InputHost();
        host.Command.Option<string>("first", "First.").Default("first-secret").Sensitive()
            .Validate("Throws.", _ => throw new Exception("first-secret later-secret"));
        host.Command.Option<string>("later", "Later.").Sensitive().FromEnvironment("LATER");
        host.Environment["LATER"] = "later-secret";
        (await host.Command.RunAsync([], _ => throw new Exception("must not run"))).Should().Be(1);
        host.Error.ToString().Should().Contain("[REDACTED] [REDACTED]")
            .And.NotContain("first-secret").And.NotContain("later-secret");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public async Task Rejected_values_are_not_echoed_and_work_is_not_entered(string raw)
    {
        using var host = new InputHost();
        host.Command.Option<string>("token", "Token.").Sensitive()
            .Validate("Use eight characters.", value => value.Length >= 8);
        (await host.Command.RunAsync(["--token=" + raw], _ => throw new Exception("must not run"))).Should().Be(2);
        host.Error.ToString().Should().Contain("--token: Use eight characters.");
        host.Output.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData("state")]
    [InlineData("predicate")]
    [InlineData("body")]
    [InlineData("cleanup")]
    public async Task Exception_presentation_masks_outer_and_inner_details_without_mutating_exception(string stage)
    {
        using var host = new InputHost();
        const string raw = "owned-secret";
        var original = new InvalidOperationException("outer " + raw, new IOException("inner " + raw));
        host.Command.Option<string>("token", "Token.").Sensitive().Default(raw);
        if (stage == "state")
        {
            host.Command.State<object>(() => throw original);
        }
        var target = host.Command.Target("work", "Work.")
            .When("Enabled.", _ => stage == "predicate" ? throw original : true)
            .Run((Action<RafterContext>)(context =>
            {
                context.WriteLine(raw);
                if (stage == "body")
                {
                    throw original;
                }
            }));
        host.Command.Cleanup("Release.", context =>
        {
            context.WriteLine(raw);
            if (stage == "cleanup")
            {
                throw original;
            }
        });
        (await host.Command.RunAsync(["--quiet"], target)).Should().Be(1);
        host.Error.ToString().Should().Contain("outer [REDACTED]").And.Contain("inner [REDACTED]")
            .And.NotContain(raw);
        host.Output.ToString().Should().NotContain(raw);
        original.Message.Should().Be("outer " + raw);
        original.InnerException!.Message.Should().Be("inner " + raw);
    }

    [Fact]
    public async Task Snapshots_keep_late_sensitivity_changes_out_of_the_current_invocation()
    {
        using var host = new InputHost();
        var late = host.Command.Option<string>("late", "Late.").Default("late-secret");
        host.Command.Option<string>("trigger", "Trigger.").Default("value").Validate("Edit.", _ =>
        {
            late.Sensitive();
            return true;
        });
        (await host.Command.RunAsync(["--quiet"], context => context.WriteLine(context.GetValue(late)))).Should().Be(0);
        (await host.Command.RunAsync(["--quiet"], context => context.WriteLine(context.GetValue(late)))).Should().Be(0);
        host.Output.ToString().Should().Be("late-secret" + Environment.NewLine + "[REDACTED]" + Environment.NewLine);
    }

    [Fact]
    public async Task Overlapping_and_repeated_runs_do_not_share_sensitive_candidates()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var command = new Command("Isolation.", () => Host(output, error));
        var token = command.Option<string>("token", "Token.").Required().Sensitive();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = command.RunAsync(["--quiet", "--token", "first-secret"], async context =>
        {
            await release.Task;
            context.GetValue(token).Should().Be("first-secret");
            context.WriteLine("first-secret second-secret");
        });
        try
        {
            (await command.RunAsync(["--quiet", "--token", "second-secret"], context =>
                context.WriteLine("first-secret second-secret"))).Should().Be(0);
        }
        finally
        {
            release.TrySetResult();
        }
        (await first).Should().Be(0);
        output.ToString().Should().Be("first-secret [REDACTED]" + Environment.NewLine
            + "[REDACTED] second-secret" + Environment.NewLine);
    }

    [Fact]
    public async Task Fallback_error_reporting_masks_values_even_when_the_renderer_has_failed()
    {
        using var output = new StringWriter();
        using var error = new FailOnceWriter();
        var command = new Command("Fallback.", () => Host(output, error));
        command.Option<string>("token", "Token.").Sensitive().Default("fallback-secret");
        (await command.RunAsync(["--quiet"], (Action<RafterContext>)(_ =>
            throw new Exception("fallback-secret")))).Should().Be(1);
        error.ToString().Should().Contain("[REDACTED]").And.NotContain("fallback-secret");
    }

    [Fact]
    public async Task Parse_failure_fallback_keeps_previously_collected_CLI_and_environment_values_masked()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var command = new Command("Lookup.", () => Host(output, error) with
        {
            ReadEnvironment = name => name == "FIRST" ? "environment-secret"
                : throw new Exception("environment-secret cli-secret default-secret")
        });
        command.Option<string>("cli", "CLI.").Sensitive().Default("default-secret");
        command.Option<string>("first", "First.").Sensitive().FromEnvironment("FIRST");
        command.Option<string>("last", "Last.").FromEnvironment("LAST");
        (await command.RunAsync(["--cli", "cli-secret"], _ => { })).Should().Be(1);
        error.ToString().Should().Contain("[REDACTED] [REDACTED] [REDACTED]")
            .And.NotContain("environment-secret").And.NotContain("cli-secret").And.NotContain("default-secret");
    }

    [Fact]
    public async Task Process_stream_text_remains_raw_while_framework_metadata_and_explicit_WriteLine_are_masked()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var values = new SensitiveValues();
        values.Add("process-secret");
        var presentation = new InvocationOutput(Host(output, error), new(false, false, true, true, []), values);
        presentation.ProcessStarted("process-secret", "/process-secret");
        presentation.ProcessOutput("process-secret\n", false);
        presentation.ProcessOutput("process-secret\n", true);
        presentation.WriteLine("process-secret");
        await presentation.CompleteOutputAsync();
        await presentation.CompleteErrorAsync();
        output.ToString().Should().Be("process-secret\n[REDACTED]" + Environment.NewLine);
        error.ToString().Should().Contain("Process | [REDACTED]")
            .And.Contain("Working directory: /[REDACTED]").And.EndWith("process-secret\n");
    }

    [Fact]
    public async Task Complete_styled_messages_are_masked_before_wrapping()
    {
        using var output = new StringWriter();
        var values = new SensitiveValues();
        values.Add("prefix-secret-suffix");
        var destination = new RendererDestination(output, new(false, true), 8);
        var paragraph = new PresentationParagraph(values, "prefix-", new Style(Color.Red))
            .Append("secret-", new Style(Color.Blue)).Append("suffix");
        destination.Layout(paragraph);
        await destination.CompleteAsync();
        string.Concat(output.ToString().Split(Environment.NewLine)).Should().Be("[REDACTED]");
    }

    [Theory]
    [InlineData("secret secret SECRET", "[REDACTED] [REDACTED] SECRET", "secret")]
    [InlineData("abc-secret-abc", "[REDACTED]-[REDACTED]", "abc", "abc-secret")]
    [InlineData("secret\nvalue!", "[REDACTED]!", "secret\nvalue")]
    [InlineData("x x", "[REDACTED] [REDACTED]", "x")]
    [InlineData("a b", "a[REDACTED]b", " ")]
    [InlineData("unchanged", "unchanged", "")]
    [InlineData("secret REDACTED", "[REDACTED] [REDACTED]", "secret", "REDACTED")]
    public void Exact_matching_is_ordinal_longest_at_a_position_and_never_remasks_replacements(
        string text, string expected, params string[] patterns)
    {
        var values = new SensitiveValues();
        foreach (var pattern in patterns.Concat(patterns))
        {
            values.Add(pattern);
        }
        values.Redact(text).Should().Be(expected);
    }

    private static string Strip(string text) => Regex.Replace(text, "\\x1b\\[[0-9;]*m", "");

    private static InvocationHost Host(TextWriter output, TextWriter error, int width = 100, bool rich = false)
        => new(Directory.GetCurrentDirectory(), output, error, !rich, !rich, false, width, _ => new Subscription());

    private sealed class InputHost : IDisposable
    {
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Dictionary<string, string?> Environment { get; } = [];
        public List<string> Lookups { get; } = [];
        public Command Command { get; }

        public InputHost(int width = 100, bool rich = false)
        {
            Command = new("Sensitive inputs.", () => Host(Output, Error, width, rich) with
            {
                ReadEnvironment = name =>
                {
                    Lookups.Add(name);
                    return Environment.GetValueOrDefault(name);
                }
            });
        }

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class FailOnceWriter : StringWriter
    {
        private bool _failed;

        public override void Write(string? value)
        {
            if (!_failed)
            {
                _failed = true;
                throw new IOException("renderer failed");
            }
            base.Write(value);
        }
    }

    private sealed class Subscription : IDisposable
    {
        public void Dispose() { }
    }
}
