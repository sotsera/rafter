using AwesomeAssertions;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.IntegrationTests.Processes;

public sealed class NpmDiscoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Npm_discovery_uses_the_first_node_installation_without_silent_fallback(bool paired)
    {
        var temporary = Directory.CreateTempSubdirectory("rafter-npm-discovery-");
        try
        {
            var first = temporary.CreateSubdirectory("first node").FullName;
            var second = temporary.CreateSubdirectory("second node").FullName;
            var nodeName = OperatingSystem.IsWindows() ? "node.exe" : "node";
            File.WriteAllText(Path.Combine(first, nodeName), "fixture");
            File.WriteAllText(Path.Combine(second, nodeName), "fixture");
            var secondCli = Path.Combine(second, "node_modules", "npm", "bin", "npm-cli.js");
            Directory.CreateDirectory(Path.GetDirectoryName(secondCli)!);
            File.WriteAllText(secondCli, "fixture");
            var firstCli = Path.Combine(first, "node_modules", "npm", "bin", "npm-cli.js");
            if (paired)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(firstCli)!);
                File.WriteAllText(firstCli, "fixture");
                var installation = NpmInstallation.Find(first + Path.PathSeparator + second, temporary.FullName);
                installation.Node.Should().Be(Path.Combine(first, nodeName));
                installation.Cli.Should().Be(firstCli);
            }
            else
            {
                Action discover = () => NpmInstallation.Find(first + Path.PathSeparator + second, temporary.FullName);
                discover.Should().Throw<FileNotFoundException>().WithMessage("*first Node installation*");
            }
        }
        finally
        {
            temporary.Delete(true);
        }
    }

    [Fact]
    public void Npm_discovery_reports_missing_node_without_launching_a_shell()
    {
        Action discover = () => NpmInstallation.Find("", Directory.GetCurrentDirectory());
        discover.Should().Throw<FileNotFoundException>().WithMessage("*Node was not found*");
    }
}
