namespace Sotsera.Rafter;

/// <summary>The observed exit and complete captured output of a settled process operation.</summary>
public sealed class CapturedProcessResult : ProcessResult
{
    public CapturedStream StandardOutput { get; }
    public CapturedStream StandardError { get; }

    internal CapturedProcessResult(int exitCode, bool succeeded, CapturedStream standardOutput,
        CapturedStream standardError) : base(exitCode, succeeded)
    {
        StandardOutput = standardOutput;
        StandardError = standardError;
    }
}

/// <summary>Immutable decoded text and its recorded chunks. Empty output has empty text and chunks.</summary>
public sealed class CapturedStream
{
    internal static CapturedStream Empty { get; } = new([]);

    public string Text { get; }
    public IReadOnlyList<CapturedChunk> Chunks { get; }

    internal CapturedStream(CapturedChunk[] chunks)
    {
        Chunks = Array.AsReadOnly(chunks.ToArray());
        Text = string.Concat(chunks.Select(chunk => chunk.Text));
    }
}

/// <summary>A nonempty reader observation, not necessarily a line or a child write call.</summary>
public sealed class CapturedChunk
{
    public long Order { get; }
    public string Text { get; }

    internal CapturedChunk(long order, string text)
    {
        Order = order;
        Text = text;
    }
}

/// <summary>Identifies one redirected child output stream.</summary>
public enum ProcessOutputStream
{
    StandardOutput,
    StandardError
}
