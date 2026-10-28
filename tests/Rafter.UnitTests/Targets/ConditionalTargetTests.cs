using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Targets;

public sealed class ConditionalTargetTests
{
    [Theory]
    [InlineData(false, false, 2, 1)]
    [InlineData(true, false, 1, 2)]
    [InlineData(false, true, 3, 0)]
    [InlineData(true, true, 2, 1)]
    public async Task Conditions_run_after_shared_dependencies_and_skip_only_the_body(
        bool skipTests, bool packRequested, int completed, int skipped)
    {
        using var host = new ConditionalHost();
        var skip = host.Command.Option<bool>("skip-tests", "Skip tests.").Default(false);
        var package = host.Command.Option<bool>("pack", "Pack.").Default(false);
        var order = new List<string>();
        var prepared = false;
        var testChecks = 0;
        var packChecks = 0;
        var build = host.Command.Target("build", "Build.").Run(_ =>
        {
            prepared = true;
            order.Add("build");
        });
        var test = host.Command.Target("test", "Test.").DependsOn(build)
            .When("Tests enabled.", context =>
            {
                prepared.Should().BeTrue();
                testChecks++;
                return !context.GetValue(skip);
            }).Run(_ => { order.Add("test"); });
        var pack = host.Command.Target("pack", "Pack.")
            .When("Packaging requested.", context =>
            {
                testChecks.Should().Be(1);
                packChecks++;
                return context.GetValue(package);
            }).DependsOn(test).Run(_ => { order.Add("pack"); });
        var verify = host.Command.Target("verify", "Verify.").DependsOn(test, pack, build);
        var arguments = new List<string>();
        if (skipTests) { arguments.Add("--skip-tests"); }
        if (packRequested) { arguments.Add("--pack"); }
        (await host.Command.RunAsync([.. arguments], verify, pack, test)).Should().Be(0);
        order.Should().Equal(new[] { "build", skipTests ? null : "test", packRequested ? "pack" : null }
            .Where(name => name is not null));
        testChecks.Should().Be(1);
        packChecks.Should().Be(1);
        var error = host.Error.ToString();
        error.Should().Contain($"{completed} target bodies completed");
        if (skipped > 0) { error.Should().Contain($", {skipped} skipped"); }
        if (skipTests)
        {
            error.Should().Contain("[test] Skipped: condition not met (Tests enabled.)")
                .And.NotContain("[OK] test").And.NotContain("Test." + Environment.NewLine);
        }
        host.Output.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Invalid_conditions_are_validated_even_when_unused_for_help_or_body_invocations(int kind)
    {
        using var host = new ConditionalHost();
        var calls = 0;
        var selected = host.Command.Target("selected", "Selected.").Run(_ => { calls++; });
        var invalid = host.Command.Target("invalid", "Invalid.");
        if (kind == 5) { invalid.DependsOn(selected); }
        else { invalid.Run(_ => { calls++; }); }
        var description = kind switch { 0 => null!, 1 => "", 2 => " \t", _ => "Condition." };
        invalid.When(description, kind == 3 ? null! : _ => { calls++; return true; });
        if (kind == 4) { invalid.When("Again.", _ => true); }
        host.Command.Validate("Must not run.", _ => { calls++; return true; });
        (await host.Command.RunAsync(["--help"], selected)).Should().Be(2);
        (await host.Command.RunAsync([], selected)).Should().Be(2);
        (await host.Command.RunAsync([], _ => { calls++; })).Should().Be(2);
        calls.Should().Be(0);
        host.Error.ToString().Should().Contain("Target 'invalid'").And.Contain("condition");
        host.Output.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(48)]
    [InlineData(100)]
    public async Task Help_describes_conditions_without_evaluating_or_predicting_outcomes(int width)
    {
        using var host = new ConditionalHost(width);
        host.Command.Option<string>("required", "Required.").Required()
            .Validate("Never called.", _ => throw new Exception());
        var target = host.Command.Target("target", "Target.")
            .When("Literal [red]condition[/] 界面.", _ => throw new Exception())
            .Run((Action<RafterContext>)(_ => throw new Exception()));
        (await host.Command.RunAsync(["--help", "--quiet"], target)).Should().Be(0);
        host.Output.ToString().Should().Contain("When: Literal [red]condition[/] 界面.")
            .And.NotContain("Skipped:").And.NotContain("\x1b");
        System.Text.RegularExpressions.Regex.Replace(host.Output.ToString(), @"\s+", " ")
            .Should().Contain("conditions are checked after dependencies");
        host.Error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Conditions_do_not_hide_invalid_inputs_or_graphs(int kind)
    {
        using var host = new ConditionalHost();
        host.Command.Option<int>("count", "Count.").Default(1).Validate("Positive.", value => value > 0);
        var checks = 0;
        var target = host.Command.Target("target", "Target.")
            .When("False.", _ => { checks++; return false; }).Run(_ => { });
        if (kind == 2) { target.DependsOn(target); }
        var arguments = kind switch { 0 => new[] { "--count=bad" }, 1 => ["--count=-1"], _ => [] };
        (await host.Command.RunAsync(arguments, target)).Should().Be(2);
        checks.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 130)]
    [InlineData(3, 130)]
    [InlineData(4, 1)]
    [InlineData(5, 130)]
    public async Task Predicate_failure_or_cancellation_never_becomes_a_skip_or_starts_a_body(int kind, int expected)
    {
        using var host = new ConditionalHost();
        var bodies = 0;
        var laterChecks = 0;
        var target = host.Command.Target("target", "Target.").When("Condition.", context =>
        {
            if (kind == 0) { throw new InvalidOperationException("predicate failed"); }
            if (kind == 1) { throw new OperationCanceledException("unrelated cancellation"); }
            host.Signal();
            if (kind == 4) { throw new InvalidOperationException("failure wins"); }
            if (kind == 5) { context.CancellationToken.ThrowIfCancellationRequested(); }
            return kind == 3;
        }).Run(_ => { bodies++; });
        var later = host.Command.Target("later", "Later.").DependsOn(target)
            .When("Later.", _ => { laterChecks++; return true; }).Run(_ => { bodies++; });
        (await host.Command.RunAsync([], later)).Should().Be(expected);
        bodies.Should().Be(0);
        laterChecks.Should().Be(0);
        host.Error.ToString().Should().Contain("stopped in target").And.NotContain("Skipped:")
            .And.NotContain("[OK] target");
        if (expected == 1) { host.Error.ToString().Should().Contain("Target 'target'"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dependency_failure_or_cancellation_prevents_the_condition_from_running(bool cancel)
    {
        using var host = new ConditionalHost();
        var checks = 0;
        var dependency = host.Command.Target("dependency", "Dependency.").Run((Action<RafterContext>)(_ =>
        {
            if (cancel) { host.Signal(); }
            else { throw new InvalidOperationException("dependency failed"); }
        }));
        var target = host.Command.Target("target", "Target.").DependsOn(dependency)
            .When("Condition.", _ => { checks++; return false; }).Run(_ => { });
        (await host.Command.RunAsync([], target)).Should().Be(cancel ? 130 : 1);
        checks.Should().Be(0);
    }

    [Fact]
    public async Task Predicates_are_snapshotted_before_input_callbacks_and_reevaluated_on_later_invocations()
    {
        using var host = new ConditionalHost();
        var calls = 0;
        var checks = 0;
        var changed = false;
        var target = host.Command.Target("target", "Target.").Run(_ => { calls++; });
        host.Command.Validate("Configure next invocation.", _ =>
        {
            if (!changed)
            {
                changed = true;
                target.When("Added later.", _ => { checks++; return false; });
            }
            return true;
        });
        (await host.Command.RunAsync([], target)).Should().Be(0);
        calls.Should().Be(1);
        checks.Should().Be(0);
        (await host.Command.RunAsync([], target, target)).Should().Be(0);
        (await host.Command.RunAsync([], target)).Should().Be(0);
        calls.Should().Be(1);
        checks.Should().Be(2);
    }

    [Fact]
    public async Task Overlapping_invocations_have_independent_condition_values_and_skip_counts()
    {
        using var host = new ConditionalHost();
        var enabled = host.Command.Option<bool>("enabled", "Enabled.").Default(false);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = host.Command.Target("target", "Target.").When("Enabled.", context => context.GetValue(enabled))
            .Run(async context => { await release.Task; context.WriteLine("executed"); });
        var first = host.Command.RunAsync(["--enabled"], target);
        try
        {
            (await host.Command.RunAsync([], target, target)).Should().Be(0);
            first.IsCompleted.Should().BeFalse();
        }
        finally { release.TrySetResult(); }
        (await first).Should().Be(0);
        host.Output.ToString().Should().Be("executed" + Environment.NewLine);
        host.Error.ToString().Should().Contain("0 target bodies completed, 1 skipped")
            .And.Contain("1 target bodies completed |");
    }

    [Theory]
    [InlineData("")]
    [InlineData("--plain")]
    [InlineData("--quiet")]
    public async Task Skip_output_is_literal_on_stderr_and_quiet_hides_routine_status(string mode)
    {
        using var host = new ConditionalHost();
        var target = host.Command.Target("target", "Body must not start.")
            .When("Literal [red]false[/].", _ => false)
            .Run((Action<RafterContext>)(_ => throw new InvalidOperationException()));
        host.Command.Target("unused", "Unreachable.").When("Never evaluated.", _ => throw new Exception()).Run(_ => { });
        var group = host.Command.Target("verify", "Verify.").DependsOn(target);
        (await host.Command.RunAsync(mode.Length == 0 ? [] : [mode], group)).Should().Be(0);
        host.Output.ToString().Should().BeEmpty();
        if (mode == "--quiet") { host.Error.ToString().Should().BeEmpty(); }
        else
        {
            host.Error.ToString().Should().Contain("[target] Skipped: condition not met (Literal [red]false[/].)")
                .And.Contain("0 target bodies completed, 1 skipped")
                .And.NotContain("Body must not start.").And.NotContain("[OK] target |")
                .And.NotContain("Never evaluated").And.NotContain("\x1b");
        }
    }

    [Fact]
    public async Task Conditions_cannot_launch_body_owned_processes()
    {
        using var host = new ConditionalHost();
        var target = host.Command.Target("target", "Target.").When("No process body.", context =>
        {
            Action configure = () => context.Executable("must-not-start");
            configure.Should().Throw<InvalidOperationException>().WithMessage("*executing body*");
            return false;
        }).Run(_ => { });
        (await host.Command.RunAsync([], target)).Should().Be(0);
    }

    [Fact]
    public async Task Caught_condition_output_errors_still_fail_the_invocation_without_reporting_a_skip()
    {
        using var host = new ConditionalHost();
        var target = host.Command.Target("target", "Target.").When("Condition.", context =>
        {
            host.Output.FailWrites = true;
            try { context.WriteLine("failure"); }
            catch (IOException) { }
            return false;
        }).Run(_ => { });
        (await host.Command.RunAsync([], target)).Should().Be(1);
        host.Error.ToString().Should().NotContain("Skipped:");
    }

    private sealed class ConditionalHost : IDisposable
    {
        private readonly List<Action> _signals = [];
        public ConditionalWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public ConditionalHost(int width = 120)
        {
            Command = new("Conditional target workflow.", () => new InvocationHost("root", Output, Error,
                true, true, false, width, signal =>
                {
                    _signals.Add(signal);
                    return new Subscription(() => _signals.Remove(signal));
                }));
        }

        public void Signal()
        {
            foreach (var signal in _signals.ToArray()) { signal(); }
        }

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    private sealed class ConditionalWriter : StringWriter
    {
        public bool FailWrites { get; set; }

        public override void Write(string? value)
        {
            if (FailWrites) { throw new IOException("output failed"); }
            base.Write(value);
        }
    }
}
