using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Processes;

public sealed class ProcessCaptureOutputTests
{
    [Theory]
    [InlineData("normal")]
    [InlineData("quiet")]
    [InlineData("plain")]
    [InlineData("verbose")]
    public async Task Overflow_reports_retained_counts_without_raw_text_or_an_unexpected_stack(string mode)
    {
        using var fixture = new Fixture(mode);
        var partial = new CapturedStream([new(1, "private")]);
        fixture.Output.ProcessCaptureLimitReached("tool[red]");
        fixture.Output.Exception(new ProcessCaptureLimitException("tool[red]", ProcessOutputStream.StandardOutput,
            8, partial, CapturedStream.Empty, null), "body");
        await fixture.Output.CompleteErrorAsync();
        var text = fixture.Error.ToString();
        text.Should().Contain("Process capture limit exceeded | tool[red]")
            .And.Contain("Stream: stdout | limit: 8 UTF-16 code units")
            .And.Contain("Partial capture (UTF-16 code units): stdout 7 | stderr 0")
            .And.NotContain("private").And.NotContain("Observed exit code").And.NotContain("Exception:")
            .And.NotContain("\u001b");
        text.Contains("Capture limit exceeded | tool[red]").Should().Be(mode != "quiet");
    }

    [Fact]
    public async Task Secondary_overflow_stays_in_the_timeout_block_without_a_second_failure_or_replay()
    {
        using var fixture = new Fixture("quiet");
        fixture.Output.Exception(new CapturedProcessTimeoutException("tool", TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(10250), 0, new([new(1, "secret")]), CapturedStream.Empty,
            6, ProcessOutputStream.StandardOutput), "body");
        await fixture.Output.CompleteErrorAsync();
        var text = fixture.Error.ToString();
        text.Should().Contain("Process timed out | tool").And.Contain("Timeout: 10s | elapsed: 10s250ms")
            .And.Contain("Partial capture (UTF-16 code units): stdout 6 | stderr 0")
            .And.Contain("Capture limit also exceeded: stdout | limit: 6 UTF-16 code units")
            .And.Contain("Observed exit code: 0").And.NotContain("secret").And.NotContain("Process capture limit exceeded");
    }

    [Theory]
    [InlineData("normal", false)]
    [InlineData("quiet", false)]
    [InlineData("verbose", true)]
    public async Task Settled_cancellation_detail_is_verbose_only(string mode, bool shown)
    {
        using var fixture = new Fixture(mode);
        fixture.Output.ProcessCaptureCancelled("tool", new(new([new(1, "secret")]), CapturedStream.Empty,
            6, ProcessOutputStream.StandardOutput, ProcessStopReason.Cancellation), 0);
        await fixture.Output.CompleteErrorAsync();
        var text = fixture.Error.ToString();
        text.Contains("Process cancelled | tool").Should().Be(shown);
        text.Contains("Partial capture (UTF-16 code units): stdout 6 | stderr 0").Should().Be(shown);
        text.Should().NotContain("secret");
    }

    [Theory]
    [InlineData("normal", false)]
    [InlineData("quiet", false)]
    [InlineData("verbose", true)]
    public async Task Accepted_capture_counts_are_verbose_only_and_rejected_counts_are_complete(string mode, bool shown)
    {
        using var fixture = new Fixture(mode);
        var output = new CapturedStream([new(1, "secret")]);
        fixture.Output.ProcessCaptured(new(output, CapturedStream.Empty, 10, null, ProcessStopReason.None));
        fixture.Error.ToString().Contains("Capture (UTF-16 code units)").Should().Be(shown);
        fixture.Output.ProcessRejected(new("tool", [0], new CapturedProcessResult(7, false, output, CapturedStream.Empty)),
            ProcessFailureAction.Continue);
        await fixture.Output.CompleteErrorAsync();
        fixture.Error.ToString().Should().Contain("Capture (UTF-16 code units): stdout 6 | stderr 0")
            .And.NotContain("Partial capture").And.NotContain("secret");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly StringWriter _stdout = new();
        public StringWriter Error { get; } = new();
        public InvocationOutput Output { get; }

        public Fixture(string mode)
        {
            var host = new InvocationHost(Directory.GetCurrentDirectory(), _stdout, Error,
                true, true, false, 100, _ => throw new NotSupportedException());
            Output = new(host, new(false, mode == "quiet", mode == "verbose", mode == "plain", []));
        }

        public void Dispose()
        {
            _stdout.Dispose();
            Error.Dispose();
        }
    }
}
