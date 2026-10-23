using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Processes;

public sealed class ProcessCaptureBufferTests
{
    [Fact]
    public async Task Text_chunks_and_echo_share_one_observed_sequence_without_changing_whitespace()
    {
        using var fixture = new Fixture(20, echo: true);
        fixture.Capture.Record(" a\r\n", false);
        fixture.Capture.Record("error", true);
        fixture.Capture.Record("\nlast", false);
        await fixture.Capture.CompleteOutputAsync(Task.CompletedTask);
        var snapshot = fixture.Capture.Snapshot();
        snapshot.Output.Text.Should().Be(" a\r\n\nlast");
        snapshot.Error.Text.Should().Be("error");
        snapshot.Output.Chunks.Select(chunk => chunk.Order).Should().Equal(1, 3);
        snapshot.Error.Chunks.Select(chunk => chunk.Order).Should().Equal(2);
        fixture.Echoed.Should().Equal((" a\r\n", false), ("error", true), ("\nlast", false));
        snapshot.Output.Text.Should().Be(string.Concat(snapshot.Output.Chunks.Select(chunk => chunk.Text)));
        Action mutate = () => ((IList<CapturedChunk>)snapshot.Output.Chunks).Clear();
        mutate.Should().Throw<NotSupportedException>();
        fixture.Capture.Snapshot().Should().BeSameAs(snapshot);
    }

    [Theory]
    [InlineData("", 1, "", false)]
    [InlineData("abc", 3, "abc", false)]
    [InlineData("abcd", 3, "abc", true)]
    [InlineData("A😀", 3, "A😀", false)]
    [InlineData("A😀", 2, "A", true)]
    [InlineData("😀", 1, "", true)]
    [InlineData("A😀B", 3, "A😀", true)]
    public async Task Limit_counts_UTF16_units_and_keeps_whole_surrogate_pairs(
        string text, int limit, string retained, bool overflow)
    {
        using var fixture = new Fixture(limit);
        fixture.Capture.Record(text, false);
        await fixture.Capture.CompleteOutputAsync(Task.CompletedTask);
        var snapshot = fixture.Capture.Snapshot();
        snapshot.Output.Text.Should().Be(retained);
        snapshot.Output.Chunks.Should().OnlyContain(chunk => chunk.Text.Length > 0);
        snapshot.Error.Text.Should().BeEmpty();
        snapshot.Error.Chunks.Should().BeEmpty();
        fixture.Capture.Overflowed.Should().Be(overflow);
        snapshot.StopReason.Should().Be(overflow ? ProcessStopReason.CaptureLimit : ProcessStopReason.None);
    }

    [Fact]
    public async Task Reader_boundary_surrogates_are_held_until_recordable_and_never_publish_an_unpaired_tail()
    {
        using var fixture = new Fixture(8);
        fixture.Capture.Record("A\ud83d", false);
        fixture.Capture.Record("err", true);
        fixture.Capture.Record("\ude00B\ud83d", false);
        await fixture.Capture.CompleteOutputAsync(Task.CompletedTask);
        var snapshot = fixture.Capture.Snapshot();
        snapshot.Output.Text.Should().Be("A😀B");
        snapshot.Output.Chunks.Select(chunk => chunk.Text).Should().Equal("A", "😀B");
        snapshot.Output.Chunks.Select(chunk => chunk.Order).Should().Equal(1, 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overflow_freezes_both_streams_and_new_echo_but_keeps_queued_echo(bool error)
    {
        using var fixture = new Fixture(3, echo: true);
        fixture.Capture.Record("a", !error);
        fixture.Capture.Record("1234", error);
        fixture.Capture.Record("discard", !error);
        await fixture.Capture.CompleteOutputAsync(Task.CompletedTask);
        var snapshot = fixture.Capture.Snapshot();
        snapshot.OverflowedStream.Should().Be(error ? ProcessOutputStream.StandardError : ProcessOutputStream.StandardOutput);
        (error ? snapshot.Error.Text : snapshot.Output.Text).Should().Be("123");
        (error ? snapshot.Output.Text : snapshot.Error.Text).Should().Be("a");
        fixture.Echoed.Should().Equal(("a", !error), ("123", error));
    }

    [Fact]
    public async Task One_character_chunks_and_empty_reads_cannot_create_unbounded_metadata()
    {
        using var fixture = new Fixture(10);
        for (var index = 0; index < 100; index++)
        {
            fixture.Capture.Record("", false);
            fixture.Capture.Record("x", false);
        }
        await fixture.Capture.CompleteOutputAsync(Task.CompletedTask);
        var snapshot = fixture.Capture.Snapshot();
        snapshot.Output.Chunks.Should().HaveCount(10);
        snapshot.Output.Text.Should().Be(new string('x', 10));
    }

    [Theory]
    [InlineData("cancel", "Cancellation")]
    [InlineData("timeout", "Timeout")]
    [InlineData("overflow", "CaptureLimit")]
    public async Task First_committed_cause_wins_with_cancellation_priority_at_commitment(
        string first, string expected)
    {
        using var fixture = new Fixture(1);
        var clock = new ProcessDeadlineTests.ManualClock();
        clock.Advance(TimeSpan.FromSeconds(2));
        var deadline = new ProcessDeadline(TimeSpan.FromSeconds(1), 0, clock, fixture.Stop);
        if (first == "cancel")
        {
            fixture.Cancellation.Request();
        }
        if (first == "timeout")
        {
            deadline.Observe(false, fixture.Cancellation, () => false).Should().BeTrue();
        }
        fixture.Capture.Record("too much", false);
        deadline.Observe(false, fixture.Cancellation, () => false).Should().BeFalse();
        fixture.Cancellation.Request();
        await fixture.Capture.CompleteOutputAsync(Task.CompletedTask);
        var snapshot = fixture.Capture.Snapshot();
        snapshot.StopReason.ToString().Should().Be(expected);
        snapshot.OverflowedStream.Should().Be(ProcessOutputStream.StandardOutput);
        snapshot.Output.Text.Should().Be("t");
        deadline.Committed.Should().Be(first == "timeout");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly StringWriter _output = new();
        private readonly StringWriter _error = new();
        public List<(string Text, bool Error)> Echoed { get; } = [];
        public InvocationCancellation Cancellation { get; }
        public ProcessStopState Stop { get; } = new();
        public ProcessCapture Capture { get; }

        public Fixture(int limit, bool echo = false)
        {
            var host = new InvocationHost(Directory.GetCurrentDirectory(), _output, _error,
                true, true, false, 100, _ => throw new NotSupportedException());
            Cancellation = new(new(host, new(false, false, false, true, [])));
            Capture = new(new(limit, echo), Cancellation, Stop, (text, error) => Echoed.Add((text, error)));
        }

        public void Dispose()
        {
            Cancellation.Dispose();
            _output.Dispose();
            _error.Dispose();
        }
    }
}
