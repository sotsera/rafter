using System.Text.RegularExpressions;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Inputs;

public sealed class EnumInputTests
{
    [Theory]
    [InlineData("Debug")]
    [InlineData("DEBUG")]
    [InlineData("debug")]
    [InlineData(" \tDeBuG\r\n")]
    public async Task Member_names_ignore_case_and_surrounding_whitespace_from_either_source(string text)
    {
        foreach (var environment in new[] { false, true })
        {
            using var host = new EnumHost();
            host.Environment["CONFIGURATION"] = environment ? text : "private-invalid-fallback";
            var option = host.Command.Option<Configuration>("configuration", "Configuration.")
                .FromEnvironment("CONFIGURATION").Required().Aliases("c");
            var args = environment ? new[] { "--quiet" } : ["-c", text, "--quiet"];
            (await host.Command.RunAsync(args, context => context.GetValue(option).Should().Be(Configuration.Debug)))
                .Should().Be(0);
            host.Lookups.Should().HaveCount(environment ? 1 : 0);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("0")]
    [InlineData("20")]
    [InlineData("+20")]
    [InlineData("-1")]
    [InlineData("0x14")]
    [InlineData("18446744073709551615")]
    [InlineData("Debug, Release")]
    [InlineData("Configuration.Debug")]
    [InlineData("\"Debug\"")]
    [InlineData("private-invalid-value")]
    public async Task Rejected_input_never_falls_back_runs_callbacks_or_discloses_text(string text)
    {
        foreach (var environment in new[] { false, true })
        {
            using var host = new EnumHost();
            host.Environment["CONFIGURATION"] = environment ? text : "Release";
            host.Command.Option<Configuration>("configuration", "Configuration.").Default(Configuration.Release)
                .FromEnvironment("CONFIGURATION").Validate("Must not run.", _ => throw new Exception("validator ran"));
            host.Command.Validate("Must not run.", _ => throw new Exception("combined rule ran"));
            var args = environment ? new[] { "--verbose" } : ["--configuration=" + text, "--verbose"];
            (await host.Run(args)).Should().Be(2);
            host.Output.ToString().Should().BeEmpty();
            Regex.Replace(host.Error.ToString(), @"\s+", " ").Should().Contain("--configuration").And.Contain("declared enum member name")
                .And.NotContain("Input sources").And.NotContain("private-invalid-value")
                .And.NotContain("validator ran").And.NotContain("combined rule ran");
            host.Lookups.Should().HaveCount(environment ? 1 : 0);
            if (environment)
            {
                host.Error.ToString().Should().Contain("environment variable CONFIGURATION");
            }
        }
    }

    [Theory]
    [InlineData("case", "unique ignoring case")]
    [InlineData("alias", "distinct numeric values")]
    [InlineData("flags", "Flags enums are not supported")]
    [InlineData("default", "defaults must identify a declared member")]
    [InlineData("nullable", "without nullable value types")]
    [InlineData("empty", "at least one member name")]
    public async Task Invalid_declarations_fail_before_help_lookup_or_callbacks(string kind, string diagnostic)
    {
        using var host = new EnumHost();
        switch (kind)
        {
            case "case":
                host.Command.Option<AmbiguousCase>("configuration", "Configuration.");
                break;
            case "alias":
                host.Command.Option<SharedValue>("configuration", "Configuration.");
                break;
            case "flags":
                host.Command.Option<FlagChoices>("configuration", "Configuration.");
                break;
            case "default":
                host.Command.Option<Configuration>("configuration", "Configuration.").Default((Configuration)99);
                break;
            case "nullable":
                host.Command.Option<Configuration?>("configuration", "Configuration.");
                break;
            case "empty":
                host.Command.Option<EmptyChoices>("configuration", "Configuration.");
                break;
        }
        host.Command.Option<Configuration>("other", "Other.").FromEnvironment("OTHER");
        (await host.Run("--help")).Should().Be(2);
        host.Output.ToString().Should().BeEmpty();
        Regex.Replace(host.Error.ToString(), @"\s+", " ").Should().Contain("Invalid command").And.Contain(diagnostic);
        host.Lookups.Should().BeEmpty();
    }

    [Fact]
    public async Task Optional_required_defaulted_and_retained_handles_keep_their_contracts()
    {
        using var host = new EnumHost();
        var optional = host.Command.Option<Configuration>("optional", "Optional.")
            .Validate("Omitted values skip this rule.", _ => throw new Exception("omitted rule ran"));
        var original = host.Command.Option<Configuration>("required", "Required.");
        var required = original.Required();
        var fallback = host.Command.Option<Configuration>("fallback", "Default.").Default(Configuration.Release)
            .Validate("Defaults reach validators.", value => value == Configuration.Release);
        host.Command.Validate("Prepared types in validation.", inputs =>
        {
            Configuration? absent = inputs.GetValue(optional);
            Configuration? retained = inputs.GetValue(original);
            Configuration supplied = inputs.GetValue(required);
            Configuration defaulted = inputs.GetValue(fallback);
            return absent is null && retained == supplied && supplied == Configuration.Debug
                && defaulted == Configuration.Release;
        });
        (await host.Command.RunAsync(["--required=debug", "--quiet"], context =>
        {
            context.GetValue(optional).Should().BeNull();
            context.GetValue(original).Should().Be(Configuration.Debug);
            context.GetValue(required).Should().Be(Configuration.Debug);
            context.GetValue(fallback).Should().Be(Configuration.Release);
        })).Should().Be(0);
        (await host.Run("--quiet")).Should().Be(2);
        host.Error.ToString().Should().Contain("--required is required.");
    }

    [Fact]
    public async Task Prepared_environment_values_and_sources_stay_fixed_and_refresh_next_invocation()
    {
        using var host = new EnumHost();
        var original = host.Command.Option<Configuration>("configuration", "Configuration.")
            .FromEnvironment("CONFIGURATION");
        var configuration = original.Default(Configuration.Release);
        var second = host.Command.Option<Configuration>("other", "Other.").Required().FromEnvironment("CONFIGURATION");
        host.Environment["CONFIGURATION"] = "Debug";
        host.Command.Validate("Change after preparation.", inputs =>
        {
            host.Environment["CONFIGURATION"] = "Release";
            return inputs.GetValue(original) == inputs.GetValue(configuration)
                && inputs.GetValue(second) == inputs.GetValue(configuration);
        });
        (await host.Command.RunAsync(["--verbose", "--plain"], context =>
        {
            context.GetValue(configuration).Should().Be(Configuration.Debug);
            context.GetValue(second).Should().Be(Configuration.Debug);
        })).Should().Be(0);
        host.Lookups.Should().Equal("CONFIGURATION");
        host.Error.ToString().Should().Contain("--configuration: environment CONFIGURATION")
            .And.NotContain("Debug").And.NotContain("Release");
        (await host.Command.RunAsync(["--quiet"], context => context.GetValue(configuration).Should()
            .Be(Configuration.Release))).Should().Be(0);
        host.Environment.Remove("CONFIGURATION");
        (await host.Command.RunAsync(["--other=Release", "--quiet"], context => context.GetValue(configuration).Should()
            .Be(Configuration.Release))).Should().Be(0);
    }

    [Fact]
    public async Task Enum_rules_aggregate_with_source_context_and_prevent_combined_rules_and_body()
    {
        using var host = new EnumHost();
        host.Environment["CONFIGURATION"] = "Debug";
        host.Command.Option<Configuration>("configuration", "Configuration.").FromEnvironment("CONFIGURATION")
            .Validate("Release only.", value => value == Configuration.Release);
        host.Command.Option<Configuration>("fallback", "Fallback.").Default(Configuration.Debug)
            .Validate("Release only.", value => value == Configuration.Release);
        host.Command.Validate("Must not run.", _ => throw new Exception("combined rule ran"));
        (await host.Run("--verbose")).Should().Be(2);
        host.Error.ToString().Should().Contain("--configuration (environment CONFIGURATION): Release only.")
            .And.Contain("--fallback: Release only.").And.NotContain("combined rule ran").And.NotContain("Input sources");
    }

    [Theory]
    [InlineData(48, false, false)]
    [InlineData(100, true, false)]
    [InlineData(48, true, true)]
    public async Task Help_preserves_declaration_order_wrapping_literal_text_and_metadata_order(int width, bool rich, bool plain)
    {
        using var host = new EnumHost(width, rich);
        host.Environment["CONFIGURATION"] = "private-invalid-value";
        host.Command.Option<Configuration>("configuration", "Build [red]literal[/].").Aliases("c")
            .Default(Configuration.Release).FromEnvironment("CONFIGURATION")
            .Validate("Rule [red]literal[/].", _ => throw new Exception("validator ran"));
        host.Command.Option<Configuration>("optional", "Optional.");
        host.Command.Option<Configuration>("required", "Required.").Required();
        (await host.Run(plain ? ["--help", "--plain"] : ["--help"])).Should().Be(0);
        host.Lookups.Should().BeEmpty();
        host.Error.ToString().Should().BeEmpty();
        var raw = host.Output.ToString();
        var help = Regex.Replace(raw, "\\x1b\\[[0-9;]*m", "");
        var folded = Regex.Replace(help, @"\s+", " ");
        folded.Should().Contain("--configuration, -c <value>").And.Contain("Build [red]literal[/]. Default: Release.")
            .And.Contain("Allowed: Debug, Release, ExtremelyLongConfigurationForWrapping (case-insensitive).")
            .And.Contain("Environment: CONFIGURATION.").And.Contain("Optional; no default.").And.Contain("Required.")
            .And.NotContain("<Configuration>").And.NotContain("private-invalid-value");
        help.IndexOf("Allowed:", StringComparison.Ordinal).Should()
            .BeLessThan(help.IndexOf("Environment:", StringComparison.Ordinal));
        help.IndexOf("Environment:", StringComparison.Ordinal).Should()
            .BeLessThan(help.IndexOf("Rules:", StringComparison.Ordinal));
        help.Split(Environment.NewLine).Should().OnlyContain(line => line.Length <= width);
        var choiceBlock = help[help.IndexOf("    Allowed:", StringComparison.Ordinal)..help.IndexOf("    Environment:", StringComparison.Ordinal)];
        choiceBlock.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should()
            .OnlyContain(line => line.StartsWith("    ", StringComparison.Ordinal));
        if (plain || !rich)
        {
            raw.Should().NotContain("\x1b[");
        }
    }

    [Theory]
    [InlineData("--configuration")]
    [InlineData("--configuration=not-a-member")]
    [InlineData("--Configuration=Debug")]
    public async Task Malformed_CLI_is_rejected_even_with_help(string argument)
    {
        using var host = new EnumHost();
        host.Command.Option<Configuration>("configuration", "Configuration.");
        (await host.Run(argument, "--help")).Should().Be(2);
        host.Output.ToString().Should().BeEmpty();
        host.Error.ToString().Should().NotContain("not-a-member");
    }

    [Fact]
    public async Task All_CSharp_enum_underlying_types_accept_named_extreme_values()
    {
        await VerifyUnderlyingAsync(ByteChoice.High);
        await VerifyUnderlyingAsync(SByteChoice.High);
        await VerifyUnderlyingAsync(ShortChoice.High);
        await VerifyUnderlyingAsync(UShortChoice.High);
        await VerifyUnderlyingAsync(IntChoice.High);
        await VerifyUnderlyingAsync(UIntChoice.High);
        await VerifyUnderlyingAsync(LongChoice.High);
        await VerifyUnderlyingAsync(ULongChoice.High);
    }

    private static async Task VerifyUnderlyingAsync<T>(T expected) where T : struct, Enum
    {
        using var host = new EnumHost();
        host.Environment["VALUE"] = "HIGH";
        var option = host.Command.Option<T>("value", "Value.").Default(expected).FromEnvironment("VALUE");
        (await host.Command.RunAsync(["--quiet"], context => context.GetValue(option).Should().Be(expected))).Should().Be(0);
        (await host.Command.RunAsync(["--value=high", "--quiet"], context => context.GetValue(option).Should().Be(expected)))
            .Should().Be(0);
    }

    private sealed class EnumHost : IDisposable
    {
        public Dictionary<string, string?> Environment { get; } = new(StringComparer.Ordinal);
        public List<string> Lookups { get; } = [];
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public EnumHost(int width = 100, bool rich = false)
        {
            Command = new("Enum inputs.", () => new InvocationHost("root", Output, Error,
                !rich, !rich, false, width, _ => new Subscription())
            {
                ReadEnvironment = name =>
                {
                    Lookups.Add(name);
                    return Environment.GetValueOrDefault(name);
                }
            });
        }

        public Task<int> Run(params string[] args) => Command.RunAsync(args, context => context.WriteLine("body ran"));

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

    private enum Configuration
    {
        Debug = 20,
        Release = -1,
        ExtremelyLongConfigurationForWrapping = 0
    }

    private enum AmbiguousCase
    {
        Debug,
        DEBUG
    }

    private enum SharedValue
    {
        Debug = 0,
        Release = 0
    }

    [Flags]
    private enum FlagChoices
    {
        Debug = 1,
        Release = 2
    }

    private enum EmptyChoices { }

    private enum ByteChoice : byte
    {
        High = byte.MaxValue
    }

    private enum SByteChoice : sbyte
    {
        High = sbyte.MinValue
    }

    private enum ShortChoice : short
    {
        High = short.MinValue
    }

    private enum UShortChoice : ushort
    {
        High = ushort.MaxValue
    }

    private enum IntChoice : int
    {
        High = int.MinValue
    }

    private enum UIntChoice : uint
    {
        High = uint.MaxValue
    }

    private enum LongChoice : long
    {
        High = long.MinValue
    }

    private enum ULongChoice : ulong
    {
        High = ulong.MaxValue
    }
}
