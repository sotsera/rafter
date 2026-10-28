namespace Sotsera.Rafter;

/// <summary>Creates typed npm commands launched directly through Node.</summary>
public static class NpmExtensions
{
    /// <summary>Finds a standard paired Node/npm installation on PATH. Never invokes an npm shell wrapper.</summary>
    public static NpmExecutable Npm(this RafterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var installation = Internal.NpmInstallation.Find(Environment.GetEnvironmentVariable("PATH") ?? "",
            context.RootDirectory);
        return context.Npm(installation.Node, installation.Cli);
    }

    /// <summary>Uses explicit Node and npm-cli.js paths for custom installations. Relative paths use the invocation root.</summary>
    public static NpmExecutable Npm(this RafterContext context, string nodePath, string npmCliPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        Internal.ProcessConfiguration.CheckText(npmCliPath, nameof(npmCliPath));
        return new(context.Executable(nodePath), context.ResolvePath(npmCliPath));
    }
}
