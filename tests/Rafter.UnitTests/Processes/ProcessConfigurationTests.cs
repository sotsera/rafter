using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Processes;

public sealed class ProcessConfigurationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task Invalid_capture_limits_throw_synchronously_without_admitting_work(int limit)
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync([], context =>
        {
            var process = context.Process("must-not-launch");
            Task<CapturedProcessResult>? operation = null;
            Action invalid = () => operation = process.RunAndCaptureAsync(limit);
            invalid.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("maxCharactersPerStream");
            operation.Should().BeNull();
            process.OnFailure(_ => ProcessFailureAction.Continue);
        })).Should().Be(0);
        host.Error.ToString().Should().NotContain("Process |").And.NotContain("unfinished");
    }

    [Fact]
    public async Task Capture_handler_conflicts_throw_before_admission_even_after_cancellation()
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync([], context =>
        {
            var process = context.Process("must-not-launch").OnFailure(_ => ProcessFailureAction.Continue);
            host.Signal();
            Action invalid = () => process.RunAndCaptureAsync(1, onFailure: _ => ProcessFailureAction.Continue)
                .GetAwaiter().GetResult();
            invalid.Should().Throw<InvalidOperationException>();
        })).Should().Be(130);
        host.Error.ToString().Should().NotContain("Process |").And.NotContain("unfinished");
    }

    [Fact]
    public async Task Validated_capture_after_cancellation_has_empty_typed_partials_and_the_invocation_token()
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            host.Signal();
            Func<Task> run = async () => { await context.Process("must-not-launch").RunAndCaptureAsync(8); };
            var cancelled = (await run.Should().ThrowAsync<CapturedProcessCanceledException>()).Which;
            cancelled.PartialStandardOutput.Text.Should().BeEmpty();
            cancelled.PartialStandardError.Chunks.Should().BeEmpty();
            cancelled.CaptureLimit.Should().Be(8);
            cancelled.CancellationToken.Should().Be(context.CancellationToken);
            cancelled.ObservedExitCode.Should().BeNull();
        })).Should().Be(130);
        host.Error.ToString().Should().NotContain("Process |").And.NotContain("unfinished");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Invalid_configuration_is_catchable_before_launch(int kind)
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            Action configure = kind switch
            {
                0 => () => context.Process(" "),
                1 => () => context.Process("dotnet").Arguments("nul\0token"),
                _ => () => context.Process("dotnet").OnFailure(_ => ProcessFailureAction.Fail)
                    .OnFailure(_ => ProcessFailureAction.Continue)
            };
            configure.Should().Throw<Exception>();
        })).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public async Task Invalid_or_repeated_configuration_is_rejected_before_launch(int kind)
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var process = context.Process("dotnet");
            Action configure = kind switch
            {
                0 => () => process.AcceptExitCodes(null!),
                1 => () => process.AcceptExitCodes(),
                2 => () => process.AcceptExitCodes(0).AcceptExitCodes(0),
                3 => () => process.WorkingDirectory(".").WorkingDirectory("."),
                4 => () => process.Environment("KEY", "value").Environment("KEY", "value"),
                5 => () => process.Environment("KEY", "value").Environment("key", "different"),
                6 => () => process.Environment("BAD=NAME", "value"),
                7 => () => process.Environment("KEY", null!),
                8 => () => process.Environment("KEY", "bad\0value"),
                _ => () => process.WorkingDirectory(" ")
            };
            configure.Should().Throw<Exception>();
        })).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public async Task Invalid_timeout_does_not_mutate_builder_and_positive_full_range_is_valid(long ticks)
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            var process = context.Process("must-not-launch");
            Action invalid = () => process.Timeout(TimeSpan.FromTicks(ticks));
            invalid.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("timeout");
            process.Timeout(TimeSpan.MaxValue).Should().BeSameAs(process);
            var alias = process;
            Action duplicate = () => alias.Timeout(TimeSpan.MaxValue);
            duplicate.Should().Throw<InvalidOperationException>();
        })).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Unhandled_timeout_configuration_error_is_body_failure_before_launch()
    {
        using var host = new ProcessHost();
        (await host.Command.RunAsync(["--quiet"], context =>
        {
            context.Process("must-not-launch").Timeout(TimeSpan.Zero);
        })).Should().Be(1);
        host.Error.ToString().Should().Contain("ArgumentOutOfRangeException").And.NotContain("Process |");
    }

    private sealed class ProcessHost : IDisposable
    {
        private Action? _signal;
        public StringWriter Output { get; }
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public ProcessHost(StringWriter? output = null, string? root = null)
        {
            Output = output ?? new StringWriter();
            Command = new("Process workflow.", () => new InvocationHost(root ?? Directory.GetCurrentDirectory(), Output, Error,
                true, true, false, 100, signal =>
                {
                    _signal = signal;
                    return new Subscription(() => _signal = null);
                }));
        }

        public void Signal() => _signal!();

        public void Dispose()
        {
            TestContext.Current.TestOutputHelper?.WriteLine("Rafter stdout:\n{0}\nRafter stderr:\n{1}", Output, Error);
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

}
