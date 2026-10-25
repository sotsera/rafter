using System.Text;
using AwesomeAssertions;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.UnitTests.Output;

public sealed class TerminalCoordinationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Shared_terminal_headings_and_prefixes_follow_the_physical_line(bool rich, bool reverse)
    {
        using var writer = new StringWriter();
        var state = new DestinationState();
        var stdout = new RendererDestination(writer, new(rich, true), state: state);
        var stderr = new RendererDestination(writer, new(rich, true), state: state);
        var first = reverse ? stderr : stdout;
        var second = reverse ? stdout : stderr;
        first.Literal("partial");
        second.Framework("heading", Spectre.Console.Style.Plain);
        first.Literal("next");
        second.Literal(" continuation\n");
        first.Literal("last\n");
        await stdout.CompleteAsync();
        await stderr.CompleteAsync();
        var text = System.Text.RegularExpressions.Regex.Replace(writer.ToString(), "\u001b\\[[0-9;]*m", "");
        var prefix = rich ? "  > " : "";
        text.Should().Be(prefix + "partial" + Environment.NewLine + "heading" + Environment.NewLine
            + prefix + "next continuation\n" + prefix + "last\n");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Invocation_shares_line_state_only_when_both_streams_are_terminal(bool redirectOut, bool redirectError)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var renderer = new InvocationOutput(new("root", stdout, stderr, redirectOut, redirectError, false, 80,
            _ => throw new NotSupportedException()), new(false, false, false, true, []));
        renderer.ProcessOutput("partial", false);
        renderer.Invalid("heading", []);
        await renderer.CompleteOutputAsync();
        await renderer.CompleteErrorAsync();
        stdout.ToString().Should().Be("partial");
        stderr.ToString().Should().Be((redirectOut || redirectError ? "" : Environment.NewLine)
            + "heading" + Environment.NewLine);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Only_two_terminal_destinations_coordinate_their_writes(bool outputRedirected, bool errorRedirected)
    {
        using var output = new BlockingWriter();
        using var error = new ObservedWriter();
        var host = new InvocationHost("root", output, error, outputRedirected, errorRedirected, false, 80,
            _ => throw new NotSupportedException());
        var renderer = new InvocationOutput(host, new(false, false, false, true, []));
        var first = Task.Factory.StartNew(() => renderer.ProcessOutput("stdout\n", error: false),
            TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task? second = null;
        try
        {
            await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            second = Task.Factory.StartNew(() =>
            {
                secondStarted.SetResult();
                renderer.ProcessOutput("stderr\n", error: true);
            }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (outputRedirected || errorRedirected)
            {
                await error.Written.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            else
            {
                var delay = Task.Delay(150, TestContext.Current.CancellationToken);
                (await Task.WhenAny(error.Written.Task, delay)).Should().Be(delay,
                    "the second terminal write must wait until the first render operation has finished");
            }
        }
        finally
        {
            output.Release.Set();
            await Task.WhenAll(first, second ?? Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        await renderer.CompleteOutputAsync();
        await renderer.CompleteErrorAsync();
        error.Written.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class BlockingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();

        public override void Write(string? value)
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Test writer was not released.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Release.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class ObservedWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public TaskCompletionSource Written { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Write(string? value) => Written.TrySetResult();
    }
}
