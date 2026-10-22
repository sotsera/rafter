using AwesomeAssertions;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Processes;

public sealed class ProcessDeadlineTests
{
    [Fact]
    public void Completion_recheck_wins_over_a_delayed_deadline_observation_and_stays_disarmed()
    {
        using var fixture = new Fixture();
        var deadline = new ProcessDeadline(TimeSpan.FromSeconds(1), fixture.Clock.GetTimestamp(), fixture.Clock);
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        deadline.Observe(false, fixture.Cancellation, () => true).Should().BeFalse();
        deadline.ExecutionCompleted.Should().BeTrue();
        deadline.Observe(false, fixture.Cancellation, () => throw new Exception("Already disarmed"))
            .Should().BeFalse();
        deadline.Committed.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancellation_before_or_during_recheck_prevents_timeout_commitment(bool duringRecheck)
    {
        using var fixture = new Fixture();
        var deadline = new ProcessDeadline(TimeSpan.FromTicks(1), fixture.Clock.GetTimestamp(), fixture.Clock);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        if (!duringRecheck)
        {
            fixture.Cancellation.Request();
        }
        deadline.Observe(false, fixture.Cancellation, () =>
        {
            fixture.Cancellation.Request();
            return false;
        }).Should().BeFalse();
        deadline.Committed.Should().BeFalse();
    }

    [Fact]
    public void Committed_timeout_survives_later_cancellation_and_completion_without_committing_twice()
    {
        using var fixture = new Fixture();
        var deadline = new ProcessDeadline(TimeSpan.FromSeconds(1), fixture.Clock.GetTimestamp(), fixture.Clock);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        deadline.Observe(false, fixture.Cancellation, () => false).Should().BeTrue();
        fixture.Cancellation.Request();
        deadline.Observe(true, fixture.Cancellation, () => throw new Exception("Already committed"))
            .Should().BeFalse();
        deadline.Committed.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Omitted_or_maximum_timeout_does_not_impose_a_timer_range_limit(bool maximum)
    {
        using var fixture = new Fixture();
        var deadline = new ProcessDeadline(maximum ? TimeSpan.MaxValue : null,
            fixture.Clock.GetTimestamp(), fixture.Clock);
        fixture.Clock.Advance(TimeSpan.FromDays(100000));
        deadline.Observe(false, fixture.Cancellation, () => throw new Exception("Not expired"))
            .Should().BeFalse();
    }

    internal sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);
        public void Advance(TimeSpan value) => Interlocked.Add(ref _timestamp, value.Ticks);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly StringWriter _writer = new();
        public ManualClock Clock { get; } = new();
        public InvocationCancellation Cancellation { get; }

        public Fixture()
        {
            var host = new InvocationHost(Directory.GetCurrentDirectory(), _writer, _writer,
                true, true, false, 100, _ => throw new NotSupportedException());
            Cancellation = new(new InvocationOutput(host, new(false, true, false, true, [])));
        }

        public void Dispose()
        {
            Cancellation.Dispose();
            _writer.Dispose();
        }
    }
}
