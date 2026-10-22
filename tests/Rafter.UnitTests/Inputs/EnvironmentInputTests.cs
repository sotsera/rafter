using System.Text.RegularExpressions;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Inputs;

public sealed class EnvironmentInputTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" KEY")]
    [InlineData("KEY ")]
    [InlineData("KEY=VALUE")]
    [InlineData("KEY\0TAIL")]
    [InlineData("KEY\nTAIL")]
    [InlineData("KEY\u007f")]
    public async Task Invalid_names_are_declaration_errors_before_help_or_lookup(string? name)
    {
        using var host = new InputHost();
        host.Command.Option<string>("text", "Text.").FromEnvironment(name!);
        (await host.Run("--help")).Should().Be(2);
        host.Lookups.Should().BeEmpty();
        host.Output.ToString().Should().BeEmpty();
        host.Error.ToString().Should().Contain("Invalid command").And.Contain("Environment names");
    }

    [Theory]
    [InlineData("lowercase")]
    [InlineData("Mixed_Case")]
    [InlineData("ProgramFiles(x86)")]
    [InlineData("a.b-c")]
    [InlineData("two words")]
    public async Task Names_are_preserved_without_CLI_grammar_or_case_conversion(string name)
    {
        using var host = new InputHost();
        host.Environment[name] = "value";
        var option = host.Command.Option<string>("text", "Text.").FromEnvironment(name).Required();
        (await host.Command.RunAsync(["--quiet"], context => context.GetValue(option).Should().Be("value")))
            .Should().Be(0);
        host.Lookups.Should().Equal(name);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Repeated_mappings_are_rejected_across_retained_and_finalized_handles(bool required, bool different)
    {
        using var host = new InputHost();
        var original = host.Command.Option<int>("count", "Count.").FromEnvironment("FIRST");
        var finalized = required ? original.Required() : original.Default(1);
        finalized.FromEnvironment(different ? "SECOND" : "FIRST");
        (await host.Run("--help")).Should().Be(2);
        host.Lookups.Should().BeEmpty();
        host.Error.ToString().Should().Contain("FromEnvironment may be configured only once.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Ada ")]
    [InlineData("\"Ada\"")]
    [InlineData("literal\\n[red]text[/]")]
    public async Task Required_strings_preserve_empty_whitespace_quotes_and_escapes(string text)
    {
        using var host = new InputHost();
        host.Environment["NAME"] = text;
        var option = host.Command.Option<string>("name", "Name.").Required().FromEnvironment("NAME");
        (await host.Command.RunAsync(["--quiet"], context => context.GetValue(option).Should().Be(text)))
            .Should().Be(0);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("FALSE", false)]
    [InlineData(" \tTrue\r\n", true)]
    [InlineData(" false ", false)]
    public async Task Boolean_words_ignore_case_and_surrounding_whitespace(string text, bool expected)
    {
        using var host = new InputHost();
        host.Environment["FLAG"] = text;
        var option = host.Command.Option<bool>("flag", "Flag.").FromEnvironment("FLAG").Required();
        (await host.Command.RunAsync([], context => context.GetValue(option).Should().Be(expected))).Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("on")]
    [InlineData("off")]
    [InlineData("\"true\"")]
    [InlineData("true\0")]
    public async Task Invalid_Boolean_values_never_fall_back_or_run_callbacks(string text)
    {
        using var host = new InputHost();
        host.Environment["FLAG"] = text;
        host.Command.Option<bool>("flag", "Flag.").Default(true).FromEnvironment("FLAG")
            .Validate("Must not run.", _ => throw new InvalidOperationException("validator ran"));
        (await host.Command.RunAsync(["--verbose"], _ => throw new InvalidOperationException("body ran")))
            .Should().Be(2);
        host.Error.ToString().Should().Contain("environment variable FLAG").And.Contain("expected true or false")
            .And.NotContain("Input sources").And.NotContain("validator ran").And.NotContain("body ran");
    }

    [Theory]
    [InlineData(" 42 ", 42)]
    [InlineData("-42", -42)]
    [InlineData("+0", 0)]
    public async Task Integers_convert_before_individual_and_combined_validation(string text, int expected)
    {
        using var host = new InputHost();
        host.Environment["COUNT"] = text;
        var option = host.Command.Option<int>("count", "Count.").Default(1).FromEnvironment("COUNT")
            .Validate("Converted value.", value => value == expected);
        host.Command.Validate("Combined value.", inputs => inputs.GetValue(option) == expected);
        (await host.Command.RunAsync([], context => context.GetValue(option).Should().Be(expected))).Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2147483648")]
    [InlineData("private-bad-value")]
    [InlineData("\"42\"")]
    public async Task Invalid_integer_errors_identify_the_source_without_echoing_values(string text)
    {
        using var host = new InputHost();
        host.Environment["COUNT"] = text;
        host.Command.Option<int>("count", "Count.").FromEnvironment("COUNT").Default(1);
        (await host.Run("--quiet")).Should().Be(2);
        host.Error.ToString().Should().Contain("COUNT").And.Contain("expected an integer")
            .And.NotContain("private-bad-value");
    }

    [Fact]
    public async Task Command_line_and_aliases_win_without_reading_the_environment()
    {
        using var host = new InputHost { FailLookup = true };
        var name = host.Command.Option<string>("name", "Name.").Aliases("n").FromEnvironment("NAME").Required();
        var count = host.Command.Option<int>("count", "Count.").FromEnvironment("COUNT").Default(1);
        var flag = host.Command.Option<bool>("flag", "Flag.").FromEnvironment("FLAG");
        (await host.Command.RunAsync(["-n=", "--count=0", "--flag=false", "--verbose"], context =>
        {
            context.GetValue(name).Should().BeEmpty();
            context.GetValue(count).Should().Be(0);
            context.GetValue(flag).Should().BeFalse();
        })).Should().Be(0);
        host.Lookups.Should().BeEmpty();
        host.Error.ToString().Should().Contain("--name: command line").And.NotContain("environment NAME");
    }

    [Fact]
    public async Task Missing_variables_use_defaults_or_remain_optional_and_required_still_fails()
    {
        using var host = new InputHost();
        var count = host.Command.Option<int>("count", "Count.").Default(7).FromEnvironment("COUNT");
        var flag = host.Command.Option<bool>("flag", "Flag.").FromEnvironment("FLAG");
        (await host.Command.RunAsync(["--verbose"], context =>
        {
            context.GetValue(count).Should().Be(7);
            context.GetValue(flag).Should().BeNull();
        })).Should().Be(0);
        host.Error.ToString().Should().Contain("--count: default").And.Contain("--flag: not supplied");
        host.Command.Option<string>("name", "Name.").FromEnvironment("NAME").Required();
        (await host.Run()).Should().Be(2);
        host.Error.ToString().Should().Contain("--name is required.");
    }

    [Fact]
    public async Task Help_never_reads_environment_or_runs_validators_and_retains_CLI_checks()
    {
        using var host = new InputHost { FailLookup = true };
        host.Command.Option<int>("count", "Count.").FromEnvironment("COUNT").Required()
            .Validate("Must not run.", _ => throw new InvalidOperationException("validator ran"));
        (await host.Run("--help")).Should().Be(0);
        host.Output.ToString().Should().Contain("Environment: COUNT.").And.Contain("Source order:");
        (await host.Run("--help", "--count=bad")).Should().Be(2);
        host.Lookups.Should().BeEmpty();
    }

    [Fact]
    public async Task Values_and_sources_stay_fixed_through_validation_and_body_but_refresh_on_the_next_run()
    {
        using var host = new InputHost();
        host.Environment["SHARED"] = "first";
        var first = host.Command.Option<string>("first", "First.").FromEnvironment("SHARED").Required();
        var second = host.Command.Option<string>("second", "Second.").Required().FromEnvironment("SHARED");
        host.Command.Validate("Mutate the host after preparation.", _ =>
        {
            host.Environment["SHARED"] = "later";
            return true;
        });
        (await host.Command.RunAsync([], context =>
        {
            context.GetValue(first).Should().Be("first");
            context.GetValue(second).Should().Be("first");
        })).Should().Be(0);
        host.Lookups.Should().Equal("SHARED");
        (await host.Command.RunAsync([], context => context.GetValue(first).Should().Be("later"))).Should().Be(0);
    }

    [Fact]
    public async Task Validator_rejections_include_source_context_and_aggregate_without_a_verbose_block()
    {
        using var host = new InputHost();
        host.Environment["COUNT"] = "-1";
        host.Environment["NAME"] = "private-value";
        host.Command.Option<int>("count", "Count.").FromEnvironment("COUNT").Validate("Positive.", value => value > 0);
        host.Command.Option<string>("name", "Name.").FromEnvironment("NAME").Validate("Rejected.", _ => false);
        (await host.Run("--verbose")).Should().Be(2);
        host.Error.ToString().Should().Contain("--count (environment COUNT): Positive.")
            .And.Contain("--name (environment NAME): Rejected.").And.NotContain("private-value")
            .And.NotContain("Input sources");
    }

    [Theory]
    [InlineData(false, false, 100)]
    [InlineData(true, false, 100)]
    [InlineData(true, true, 48)]
    public async Task Help_and_verbose_layout_preserve_sources_streams_and_plain_text(bool rich, bool plain, int width)
    {
        using var host = new InputHost(width, rich);
        host.Environment["NAME"] = "";
        var name = host.Command.Option<string>("name", "Name.").FromEnvironment("NAME").Required();
        host.Command.Option<int>("repeat", "Repeat.").FromEnvironment("COUNT").Default(1)
            .Validate("Nonnegative.", value => value >= 0);
        host.Command.Option<bool>("uppercase", "Uppercase.").FromEnvironment("FLAG");
        (await host.Command.RunAsync(plain ? ["--verbose", "--plain"] : ["--verbose"], context =>
        {
            var error = Strip(host.Error.ToString());
            error.Should().Contain("Input sources" + Environment.NewLine + "  --name: environment NAME (empty)");
            error.IndexOf("--name:", StringComparison.Ordinal).Should()
                .BeLessThan(error.IndexOf("--repeat:", StringComparison.Ordinal));
            error.Should().Contain("--repeat: default").And.Contain("--uppercase: not supplied");
            context.WriteLine("result" + context.GetValue(name));
        })).Should().Be(0);
        Strip(host.Output.ToString()).Should().Contain("result").And.NotContain("Input sources");
        host.Output.GetStringBuilder().Clear();
        (await host.Run(plain ? ["--help", "--plain"] : ["--help"])).Should().Be(0);
        var help = Strip(host.Output.ToString());
        help.IndexOf("Environment: COUNT.", StringComparison.Ordinal).Should()
            .BeLessThan(help.IndexOf("Rules:", StringComparison.Ordinal));
        help.Should().Contain("Environment: FLAG; requires true or false.");
        if (plain || !rich)
        {
            host.Output.ToString().Should().NotContain("\x1b[");
            host.Error.ToString().Should().NotContain("\x1b[");
        }
    }

    [Theory]
    [InlineData("--quiet")]
    [InlineData("--plain")]
    public async Task Routine_sources_and_resolved_contents_are_hidden_outside_verbose(string mode)
    {
        using var host = new InputHost();
        host.Environment["NAME"] = "private-environment-value";
        host.Command.Option<string>("name", "Name.").FromEnvironment("NAME").Required();
        host.Command.Option<string>("fallback", "Fallback.").Default("private-default-value");
        (await host.Run(mode)).Should().Be(0);
        host.Error.ToString().Should().NotContain("Input sources").And.NotContain("private-environment-value")
            .And.NotContain("private-default-value");
        host.Error.GetStringBuilder().Clear();
        (await host.Run("--verbose")).Should().Be(0);
        host.Error.ToString().Should().Contain("Input sources").And.NotContain("private-environment-value")
            .And.NotContain("private-default-value");
    }

    [Fact]
    public async Task Overlapping_runs_keep_independent_environment_snapshots()
    {
        using var host = new InputHost();
        var name = host.Command.Option<string>("name", "Name.").FromEnvironment("NAME").Required();
        host.Environment["NAME"] = "first";
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = host.Command.RunAsync(["--quiet"], async context =>
        {
            await release.Task;
            context.GetValue(name).Should().Be("first");
        });
        try
        {
            host.Environment["NAME"] = "second";
            (await host.Command.RunAsync(["--quiet"], context => context.GetValue(name).Should().Be("second")))
                .Should().Be(0);
        }
        finally
        {
            release.TrySetResult();
        }
        (await first).Should().Be(0);
    }

    [Fact]
    public async Task Wrapped_source_errors_keep_the_diagnostic_gutter_without_disclosing_values()
    {
        using var host = new InputHost(48);
        const string variable = "RAFTER_LONG_ENVIRONMENT_VARIABLE_FOR_COUNT";
        host.Environment[variable] = "private-invalid-count";
        host.Command.Option<int>("count", "Count.").FromEnvironment(variable);
        (await host.Run("--quiet")).Should().Be(2);
        var lines = host.Error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("Invalid input");
        lines.Skip(1).Should().OnlyContain(line => line.StartsWith("  ", StringComparison.Ordinal) && line.Length <= 48);
        host.Error.ToString().Should().NotContain("private-invalid-count");
    }

    private static string Strip(string text) => Regex.Replace(text, "\\x1b\\[[0-9;]*m", "");

    private sealed class InputHost : IDisposable
    {
        public Dictionary<string, string?> Environment { get; } = new(StringComparer.Ordinal);
        public List<string> Lookups { get; } = [];
        public bool FailLookup { get; init; }
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public InputHost(int width = 100, bool rich = false)
        {
            Command = new("Environment inputs.", () => new InvocationHost("root", Output, Error,
                !rich, !rich, false, width, _ => new Subscription())
            {
                ReadEnvironment = name =>
                {
                    Lookups.Add(name);
                    if (FailLookup)
                    {
                        throw new InvalidOperationException("Environment lookup must not run.");
                    }
                    return Environment.GetValueOrDefault(name);
                }
            });
        }

        public Task<int> Run(params string[] args) => Command.RunAsync(args, _ => { });

        public void Dispose()
        {
            TestContext.Current.TestOutputHelper?.WriteLine("stdout:\n{0}\nstderr:\n{1}", Output, Error);
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription : IDisposable
    {
        public void Dispose() { }
    }
}
