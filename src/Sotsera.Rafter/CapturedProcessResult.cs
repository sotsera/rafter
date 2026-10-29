namespace Sotsera.Rafter;

/// <summary>The observed exit and complete captured output of a settled process operation.</summary>
public sealed class CapturedProcessResult : ProcessResult
{
    /// <summary>Complete captured stdout, without presentation redaction.</summary>
    public CapturedStream StandardOutput { get; }
    /// <summary>Complete captured stderr, without presentation redaction.</summary>
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

    /// <summary>The decoded text, without presentation redaction.</summary>
    public string Text { get; }
    /// <summary>Immutable nonempty reader observations in their recorded order.</summary>
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
    /// <summary>The observation order shared by stdout and stderr for this operation; not the order of child writes.</summary>
    public long Order { get; }
    /// <summary>The decoded text, without presentation redaction.</summary>
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
    /// <summary>The child standard output stream.</summary>
    StandardOutput,
    /// <summary>The child standard error stream.</summary>
    StandardError
}
