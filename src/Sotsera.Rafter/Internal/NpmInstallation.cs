namespace Sotsera.Rafter.Internal;

internal sealed record NpmInstallation(string Node, string Cli)
{
    public static NpmInstallation Find(string path, string root)
    {
        var nodeName = OperatingSystem.IsWindows() ? "node.exe" : "node";
        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = Path.GetFullPath(entry.Trim('"'), root);
            var node = Path.Combine(directory, nodeName);
            if (!File.Exists(node))
            {
                continue;
            }
            var cli = Path.Combine(directory, "node_modules", "npm", "bin", "npm-cli.js");
            if (File.Exists(cli))
            {
                return new(node, cli);
            }
            if (!OperatingSystem.IsWindows())
            {
                var wrapper = new FileInfo(Path.Combine(directory, "npm"));
                if (wrapper.Exists && wrapper.LinkTarget is not null)
                {
                    var resolved = wrapper.ResolveLinkTarget(true);
                    if (resolved is FileInfo file && file.Exists && file.Name == "npm-cli.js")
                    {
                        return new(node, file.FullName);
                    }
                }
            }
            throw new FileNotFoundException(
                "The first Node installation on PATH has no supported npm-cli.js layout. Use context.Npm(nodePath, npmCliPath).",
                cli);
        }
        throw new FileNotFoundException("Node was not found on PATH. Use context.Npm(nodePath, npmCliPath) for an explicit installation.",
            nodeName);
    }
}
