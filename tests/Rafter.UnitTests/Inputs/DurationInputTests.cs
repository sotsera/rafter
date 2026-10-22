using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Inputs;

public sealed class DurationInputTests
{
    [Theory]
    [InlineData("250ms", 2500000L)]
    [InlineData("30s", 300000000L)]
    [InlineData("5m", 3000000000L)]
    [InlineData("1h", 36000000000L)]
    [InlineData("1h30m", 54000000000L)]
    [InlineData("2m10s", 1300000000L)]
    [InlineData("1s250ms", 12500000L)]
    [InlineData("-1h30m", -54000000000L)]
    [InlineData("+2s", 20000000L)]
    [InlineData("1h90m", 90000000000L)]
    [InlineData("1h30.5m", 54300000000L)]
    [InlineData("1.5h", 54000000000L)]
    [InlineData("0001.2500s", 12500000L)]
    [InlineData("0.0000001s", 1L)]
    [InlineData("0.0001ms", 1L)]
    [InlineData("0.000000010m", 6L)]
    [InlineData("0.000000005h", 180L)]
    [InlineData("0.0000000000000000000000000000000s", 0L)]
    [InlineData("00000000000000000000000001m", 600000000L)]
    [InlineData("\t +1H30M \r\n", 54000000000L)]
    [InlineData("1m1MS", 600010000L)]
    [InlineData("0h0m0s0ms", 0L)]
    [InlineData("-0s", 0L)]
    [InlineData("1h0.5s", 36005000000L)]
    [InlineData("922337203685477.5807ms", long.MaxValue)]
    [InlineData("-922337203685477.5808ms", long.MinValue)]
    [InlineData("256204778h48m5s477.5807ms", long.MaxValue)]
    [InlineData("-256204778h48m5s477.5808ms", long.MinValue)]
    public async Task CLI_and_environment_convert_exact_signed_compound_values(string text, long ticks)
    {
        foreach (var environment in new[] { false, true })
        {
            using var host = new DurationHost();
            host.Environment["DURATION"] = environment ? text : "private-invalid-fallback";
            var option = host.Command.Option<TimeSpan>("duration", "Duration.").Aliases("d")
                .Required().FromEnvironment("DURATION");
            var arguments = environment ? new[] { "--quiet" } : ["-d", text, "--quiet"];
            (await host.Command.RunAsync(arguments, context => context.GetValue(option).Ticks.Should().Be(ticks)))
                .Should().Be(0);
            host.Lookups.Should().HaveCount(environment ? 1 : 0);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("1")]
    [InlineData("1d")]
    [InlineData("NaNms")]
    [InlineData("Infinitys")]
    [InlineData("1e3s")]
    [InlineData("1,5s")]
    [InlineData(".5s")]
    [InlineData("1.s")]
    [InlineData("1.2.3s")]
    [InlineData("1msms")]
    [InlineData("1m1m")]
    [InlineData("1s1m")]
    [InlineData("1ms1s")]
    [InlineData("1h-30m")]
    [InlineData("1h+30m")]
    [InlineData("--1s")]
    [InlineData("+-1s")]
    [InlineData("- 1s")]
    [InlineData("1 h")]
    [InlineData("1h 30m")]
    [InlineData("00:30:00")]
    [InlineData("1.5h30m")]
    [InlineData("1.0h0m")]
    [InlineData("1s2")]
    [InlineData("0.00001ms")]
    [InlineData("0.00000001s")]
    [InlineData("0.0000000001h")]
    [InlineData("922337203685477.5808ms")]
    [InlineData("-922337203685477.5809ms")]
    [InlineData("999999999999999999999999999999999999h")]
    [InlineData("1sprivate")]
    [InlineData("private-invalid-duration")]
    [InlineData("١s")]
    [InlineData("\0s")]
    [InlineData("1s\0")]
    [InlineData("1\u00a0s")]
    public async Task Invalid_values_never_fall_back_execute_callbacks_or_disclose_rejected_text(string text)
    {
        foreach (var environment in new[] { false, true })
        {
            using var host = new DurationHost();
            host.Environment["DURATION"] = environment ? text : "30s";
            host.Command.Option<TimeSpan>("duration", "Duration.").Default(TimeSpan.FromSeconds(30))
                .FromEnvironment("DURATION").Validate("Must not run.", _ => throw new Exception("validator ran"));
            host.Command.Validate("Must not run.", _ => throw new Exception("combined rule ran"));
            var arguments = environment ? new[] { "--verbose" } : ["--duration=" + text, "--verbose"];
            (await host.Run(arguments)).Should().Be(2);
            host.Output.ToString().Should().BeEmpty();
            var error = Regex.Replace(host.Error.ToString(), @"\s+", " ");
            error.Should().Contain("--duration").And.Contain("exact duration using h, m, s, or ms in descending order")
                .And.NotContain("private-invalid-duration").And.NotContain("Input sources")
                .And.NotContain("validator ran").And.NotContain("combined rule ran");
            host.Lookups.Should().HaveCount(environment ? 1 : 0);
            if (environment)
            {
                error.Should().Contain("environment variable DURATION");
            }
        }
    }

    [Theory]
    [InlineData(0L, "0s")]
    [InlineData(1L, "0.0001ms")]
    [InlineData(-1L, "-0.0001ms")]
    [InlineData(300000000L, "30s")]
    [InlineData(900000000L, "1m30s")]
    [InlineData(54000000000L, "1h30m")]
    [InlineData(12500000L, "1s250ms")]
    [InlineData(-54000000000L, "-1h30m")]
    [InlineData(900000000000L, "25h")]
    [InlineData(10001L, "1.0001ms")]
    [InlineData(10000001L, "1s0.0001ms")]
    [InlineData(long.MaxValue, "256204778h48m5s477.5807ms")]
    [InlineData(long.MinValue, "-256204778h48m5s477.5808ms")]
    public void Canonical_format_preserves_exact_values_and_the_full_range(long ticks, string expected)
    {
        var text = DurationInputs.Format(TimeSpan.FromTicks(ticks));
        text.Should().Be(expected);
        DurationInputs.TryParse(text, out var parsed).Should().BeTrue();
        parsed.Ticks.Should().Be(ticks);
    }

    [Fact]
    public void Formatting_round_trips_values_across_the_entire_tick_range()
    {
        var random = new Random(212);
        for (var index = 0; index < 1000; index++)
        {
            var ticks = random.NextInt64(long.MinValue, long.MaxValue);
            var text = DurationInputs.Format(TimeSpan.FromTicks(ticks));
            DurationInputs.TryParse(text, out var parsed).Should().BeTrue("{0} must parse", text);
            parsed.Ticks.Should().Be(ticks);
        }
    }

    [Theory]
    [InlineData("it-IT")]
    [InlineData("ar-SA")]
    public void Conversion_and_formatting_are_invariant_under_other_current_cultures(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new(culture);
            DurationInputs.TryParse("1.25s", out var value).Should().BeTrue();
            value.Ticks.Should().Be(12500000);
            DurationInputs.Format(TimeSpan.FromTicks(10001)).Should().Be("1.0001ms");
            DurationInputs.TryParse("1,25s", out _).Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Optional_finalized_and_retained_handles_work_in_both_contexts_and_missing_required_is_invalid()
    {
        using var host = new DurationHost();
        var optional = host.Command.Option<TimeSpan>("optional", "Optional.")
            .Validate("Omitted values skip rules.", _ => throw new Exception("omitted rule ran"));
        var original = host.Command.Option<TimeSpan>("duration", "Duration.");
        var duration = original.Required();
        var fallback = host.Command.Option<TimeSpan>("fallback", "Fallback.").Default(TimeSpan.Zero)
            .Validate("Default is validated.", value => value == TimeSpan.Zero);
        host.Command.Validate("Prepared duration types.", inputs =>
        {
            TimeSpan? absent = inputs.GetValue(optional);
            TimeSpan? retained = inputs.GetValue(original);
            TimeSpan actual = inputs.GetValue(duration);
            TimeSpan defaulted = inputs.GetValue(fallback);
            return absent is null && retained == actual && actual == TimeSpan.FromMinutes(-90)
                && defaulted == TimeSpan.Zero;
        });
        (await host.Command.RunAsync(["--duration=-1h30m", "--quiet"], context =>
        {
            context.GetValue(optional).Should().BeNull();
            context.GetValue(original).Should().Be(TimeSpan.FromMinutes(-90));
            context.GetValue(duration).Should().Be(TimeSpan.FromMinutes(-90));
            context.GetValue(fallback).Should().Be(TimeSpan.Zero);
        })).Should().Be(0);
        (await host.Run("--quiet")).Should().Be(2);
        host.Error.ToString().Should().Contain("--duration is required.");
    }

    [Fact]
    public async Task Nullable_duration_types_fail_declaration_validation_before_help()
    {
        using var host = new DurationHost();
        host.Command.Option<TimeSpan?>("duration", "Duration.").FromEnvironment("DURATION");
        (await host.Run("--help")).Should().Be(2);
        Regex.Replace(host.Error.ToString(), @"\s+", " ").Should().Contain("without nullable value types");
        host.Output.ToString().Should().BeEmpty();
        host.Lookups.Should().BeEmpty();
    }

    [Fact]
    public async Task Prepared_values_are_stable_through_validation_and_refresh_on_the_next_invocation()
    {
        using var host = new DurationHost();
        host.Environment["DURATION"] = "1h30m";
        var first = host.Command.Option<TimeSpan>("first", "First.").FromEnvironment("DURATION")
            .Default(TimeSpan.FromSeconds(30));
        var second = host.Command.Option<TimeSpan>("second", "Second.").Required().FromEnvironment("DURATION");
        host.Command.Validate("Mutate after preparation.", inputs =>
        {
            host.Environment["DURATION"] = "2m";
            return inputs.GetValue(first) == inputs.GetValue(second);
        });
        (await host.Command.RunAsync(["--verbose", "--plain"], context =>
        {
            context.GetValue(first).Should().Be(TimeSpan.FromMinutes(90));
            context.GetValue(second).Should().Be(TimeSpan.FromMinutes(90));
        })).Should().Be(0);
        host.Lookups.Should().Equal("DURATION");
        host.Error.ToString().Should().Contain("--first: environment DURATION").And.NotContain("1h30m")
            .And.NotContain("2m");
        (await host.Command.RunAsync(["--quiet"], context => context.GetValue(first).Should()
            .Be(TimeSpan.FromMinutes(2)))).Should().Be(0);
    }

    [Fact]
    public async Task Validator_rejections_aggregate_with_sources_without_running_combined_rules()
    {
        using var host = new DurationHost();
        host.Environment["DURATION"] = "-1s";
        host.Command.Option<TimeSpan>("duration", "Duration.").FromEnvironment("DURATION")
            .Validate("Nonnegative.", value => value >= TimeSpan.Zero);
        host.Command.Option<TimeSpan>("fallback", "Fallback.").Default(TimeSpan.FromSeconds(-1))
            .Validate("Nonnegative.", value => value >= TimeSpan.Zero);
        host.Command.Validate("Must not run.", _ => throw new Exception("combined rule ran"));
        (await host.Run("--verbose")).Should().Be(2);
        host.Error.ToString().Should().Contain("--duration (environment DURATION): Nonnegative.")
            .And.Contain("--fallback: Nonnegative.").And.NotContain("combined rule ran").And.NotContain("Input sources");
    }

    [Theory]
    [InlineData(48, false, false)]
    [InlineData(100, true, false)]
    [InlineData(48, true, true)]
    public async Task Help_uses_canonical_defaults_units_and_existing_metadata_gutters(int width, bool rich, bool plain)
    {
        using var host = new DurationHost(width, rich);
        host.Environment["DURATION"] = "private-invalid-value";
        host.Command.Option<TimeSpan>("duration", "Delay [red]literal[/].").Aliases("d")
            .Default(TimeSpan.FromSeconds(90)).FromEnvironment("DURATION")
            .Validate("Rule [red]literal[/].", _ => throw new Exception("validator ran"));
        host.Command.Option<TimeSpan>("minimum", "Smallest.").Default(TimeSpan.MinValue);
        host.Command.Option<TimeSpan>("optional", "Optional.");
        host.Command.Option<TimeSpan>("required", "Required.").Required();
        (await host.Run(plain ? ["--help", "--plain"] : ["--help"])).Should().Be(0);
        host.Lookups.Should().BeEmpty();
        host.Error.ToString().Should().BeEmpty();
        var raw = host.Output.ToString();
        var help = Regex.Replace(raw, "\\x1b\\[[0-9;]*m", "");
        var folded = Regex.Replace(help, @"\s+", " ");
        folded.Should().Contain("--duration, -d <duration>").And.Contain("Default: 1m30s.")
            .And.Contain("Default: -256204778h48m5s477.5808ms.").And.Contain("Units: ms, s, m, h.")
            .And.Contain("Environment: DURATION.").And.Contain("Optional; no default.").And.Contain("Required.")
            .And.Contain("Delay [red]literal[/].").And.Contain("Rule [red]literal[/].")
            .And.NotContain("private-invalid-value");
        help.IndexOf("Units:", StringComparison.Ordinal).Should()
            .BeLessThan(help.IndexOf("Environment:", StringComparison.Ordinal));
        help.IndexOf("Environment:", StringComparison.Ordinal).Should()
            .BeLessThan(help.IndexOf("Rules:", StringComparison.Ordinal));
        help.Split(Environment.NewLine).Should().OnlyContain(line => line.Length <= width);
        if (plain || !rich)
        {
            raw.Should().NotContain("\x1b[");
        }
    }

    [Theory]
    [InlineData("--duration")]
    [InlineData("--duration=1m1h")]
    [InlineData("--Duration=1s")]
    public async Task Malformed_supplied_CLI_still_blocks_help(string argument)
    {
        using var host = new DurationHost();
        host.Command.Option<TimeSpan>("duration", "Duration.");
        (await host.Run(argument, "--help")).Should().Be(2);
        host.Output.ToString().Should().BeEmpty();
    }

    private sealed class DurationHost : IDisposable
    {
        public Dictionary<string, string?> Environment { get; } = new(StringComparer.Ordinal);
        public List<string> Lookups { get; } = [];
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public DurationHost(int width = 100, bool rich = false)
        {
            Command = new("Duration inputs.", () => new InvocationHost("root", Output, Error,
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
}
