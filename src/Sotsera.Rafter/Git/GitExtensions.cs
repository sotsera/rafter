namespace Sotsera.Rafter;

/// <summary>Creates typed Git commands over Rafter's generic executable API.</summary>
public static class GitExtensions
{
    /// <summary>Configures Git with normal child inheritance and current-body ownership.</summary>
    public static GitExecutable Git(this RafterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new(context.Executable("git"));
    }
}
