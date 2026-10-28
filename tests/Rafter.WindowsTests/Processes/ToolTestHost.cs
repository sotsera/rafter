using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.WindowsTests.Processes;

internal sealed class ToolTestHost : IDisposable
{
    public StringWriter Output { get; } = new();
    public StringWriter Error { get; } = new();
    public Command Command { get; }

    public ToolTestHost(string root)
    {
        Command = new("Tool adapter verification.", () => new InvocationHost(root, Output, Error,
            true, true, false, 100, _ => new Subscription()));
    }

    public void Dispose()
    {
        TestContext.Current.TestOutputHelper?.WriteLine(Output + "\n" + Error);
        Output.Dispose();
        Error.Dispose();
    }

    private sealed class Subscription : IDisposable
    {
        public void Dispose() { }
    }
}
