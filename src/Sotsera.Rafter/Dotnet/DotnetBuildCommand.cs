using System.Globalization;

namespace Sotsera.Rafter;

/// <summary>Configures a dotnet build invocation.</summary>
public sealed class DotnetBuildCommand : DotnetCommand<DotnetBuildCommand>
{
    internal DotnetBuildCommand(ProcessCommand command) : base(command) { }

    /// <summary>Limits concurrent MSBuild worker processes to a positive count.</summary>
    public DotnetBuildCommand MaxCpuCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return Arguments("-m:" + count.ToString(CultureInfo.InvariantCulture));
    }
}
