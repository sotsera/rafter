namespace Sotsera.Rafter;

/// <summary>Creates typed .NET CLI configuration over Rafter's generic executable API.</summary>
public static class DotnetExtensions
{
    /// <summary>Configures the dotnet executable with normal environment inheritance and body ownership.</summary>
    public static DotnetExecutable Dotnet(this RafterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new(context.Executable("dotnet"));
    }
}
