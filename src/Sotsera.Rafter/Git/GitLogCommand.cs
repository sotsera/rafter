using System.Globalization;

namespace Sotsera.Rafter;

/// <summary>Configures git log without changing the repository.</summary>
public sealed class GitLogCommand : ToolCommand<GitLogCommand>
{
    internal GitLogCommand(ProcessCommand command) : base(command) { }

    /// <summary>Limits the result to a positive number of commits.</summary>
    public GitLogCommand MaxCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return Arguments("--max-count=" + count.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Selects compact one-line commit output.</summary>
    public GitLogCommand Oneline() => Arguments("--oneline");
}
