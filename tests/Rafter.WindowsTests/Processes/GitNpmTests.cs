using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using Sotsera.Rafter;
using Xunit;

namespace Rafter.WindowsTests.Processes;

[Collection("Native process handles")]
public sealed class GitNpmTests
{
    [WindowsTheory]
    [InlineData("status")]
    [InlineData("log")]
    [InlineData("script")]
    [InlineData("ci")]
    public async Task Typed_tool_commands_preserve_exact_argument_vectors_and_independent_invocations(string kind)
    {
        using var host = new ToolTestHost(Directory.GetCurrentDirectory());
        (await host.Command.RunAsync(["--quiet"], async context =>
        {
            var probe = context.Executable(Path.Combine(AppContext.BaseDirectory, "ProcessProbe.exe"));
            var git = new GitExecutable(probe);
            var npm = new NpmExecutable(probe, context.File("npm cli", "npm-cli.js").FullPath);
            string[] expected;
            CapturedProcessResult captured;
            if (kind == "status")
            {
                captured = await git.Status().Short().Branch().Arguments("--", "a b").RunAndCaptureAsync(4096);
                expected = ["status", "--short", "--branch", "--", "a b"];
            }
            else if (kind == "log")
            {
                captured = await git.Log().MaxCount(3).Oneline().Arguments("--", "a b").RunAndCaptureAsync(4096);
                expected = ["log", "--max-count=3", "--oneline", "--", "a b"];
            }
            else if (kind == "script")
            {
                string[] values = ["a b", "--mode", "production"];
                var script = npm.RunScript("build").IfPresent().ScriptArguments(values).ScriptArguments("a&b|c", "");
                values[0] = "mutated";
                captured = await script.RunAndCaptureAsync(4096);
                expected = [context.File("npm cli", "npm-cli.js").FullPath, "run", "build", "--if-present", "--",
                    "a b", "--mode", "production", "a&b|c", ""];
            }
            else
            {
                captured = await npm.Ci().NoAudit().NoFund().IgnoreScripts().Arguments("--offline").RunAndCaptureAsync(4096);
                expected = [context.File("npm cli", "npm-cli.js").FullPath, "ci", "--no-audit", "--no-fund",
                    "--ignore-scripts", "--offline"];
            }
            JsonSerializer.Deserialize<string[]>(captured.StandardOutput.Text).Should().Equal(expected);
            var next = await git.Status().Porcelain().RunAndCaptureAsync(4096);
            JsonSerializer.Deserialize<string[]>(next.StandardOutput.Text).Should().Equal("status", "--porcelain=v1");
        })).Should().Be(0, host.Error.ToString());
    }

    [WindowsFact]
    public async Task Git_helpers_observe_a_real_repository_and_propagate_failures_without_changing_host_directory()
    {
        var temporary = Directory.CreateTempSubdirectory("rafter-git-tools-");
        var repository = temporary.CreateSubdirectory("repository with spaces").FullName;
        var template = temporary.CreateSubdirectory("empty template").FullName;
        var globalConfig = Path.Combine(temporary.FullName, "empty.gitconfig");
        await File.WriteAllTextAsync(globalConfig, "");
        var originalDirectory = Directory.GetCurrentDirectory();
        using var host = new ToolTestHost(temporary.FullName);
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                var git = context.Git().WorkingDirectory("repository with spaces")
                    .Environment("GIT_CONFIG_GLOBAL", globalConfig).Environment("GIT_CONFIG_NOSYSTEM", "1")
                    .Environment("GIT_AUTHOR_NAME", "Rafter fixture").Environment("GIT_AUTHOR_EMAIL", "fixture@example.invalid")
                    .Environment("GIT_COMMITTER_NAME", "Rafter fixture").Environment("GIT_COMMITTER_EMAIL", "fixture@example.invalid")
                    .Timeout(TimeSpan.FromMinutes(1));
                await git.Arguments("init", "--quiet", "--template=" + template).RunAsync();
                await File.WriteAllTextAsync(Path.Combine(repository, "a b.txt"), "first");
                await git.Arguments("add", "--", "a b.txt").RunAsync();
                await git.Arguments("-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "Fixture commit").RunAsync();
                var clean = await git.Status().Porcelain().RunAndCaptureAsync(4096);
                clean.StandardOutput.Text.Should().BeEmpty();
                await File.WriteAllTextAsync(Path.Combine(repository, "untracked.txt"), "second");
                var status = await git.Status().Short().Branch().RunAndCaptureAsync(4096);
                status.StandardOutput.Text.Should().Contain("## ").And.Contain("?? untracked.txt");
                var log = await git.Log().MaxCount(1).Oneline().RunAndCaptureAsync(4096);
                log.StandardOutput.Text.Should().Contain("Fixture commit");
                var rejected = await context.Git().Status().WorkingDirectory(temporary.FullName)
                    .RunAndCaptureAsync(4096, onFailure: _ => ProcessFailureAction.Continue);
                rejected.Succeeded.Should().BeFalse();
                rejected.StandardError.Text.Should().Contain("not a git repository");
            })).Should().Be(0, host.Error.ToString());
            Directory.GetCurrentDirectory().Should().Be(originalDirectory);
        }
        finally
        {
            // Git object files are read-only on Windows. This is only the directory allocated by this test.
            foreach (var file in temporary.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes = FileAttributes.Normal;
            }
            temporary.Delete(true);
        }
    }

    [WindowsFact]
    public async Task Npm_helpers_install_ci_and_forward_script_arguments_with_real_node_and_npm()
    {
        var temporary = Directory.CreateTempSubdirectory("rafter-npm-tools-");
        await WritePackageAsync(temporary.FullName);
        using var host = new ToolTestHost(temporary.FullName);
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                var npm = context.Npm().Timeout(TimeSpan.FromMinutes(1));
                await npm.Install().NoAudit().NoFund().IgnoreScripts().Arguments("--offline").RunAsync();
                await npm.Ci().NoAudit().NoFund().IgnoreScripts().Arguments("--offline").RunAsync();
                string[] values = ["a b", "--mode", "production", "héllo 世界", "a&b|c"];
                var captured = await npm.RunScript("echo").Arguments("--silent")
                    .ScriptArguments(values[..2]).ScriptArguments(values[2..]).RunAndCaptureAsync(4096);
                JsonSerializer.Deserialize<string[]>(captured.StandardOutput.Text).Should().Equal(values);
                var missing = await npm.RunScript("not-defined").IfPresent().RunAsync();
                missing.Succeeded.Should().BeTrue();
                var rejected = await npm.RunScript("fail").RunAndCaptureAsync(4096,
                    onFailure: _ => ProcessFailureAction.Continue);
                rejected.ExitCode.Should().Be(7);
                File.Exists(context.File("package-lock.json").FullPath).Should().BeTrue();
            })).Should().Be(0, host.Error.ToString());
        }
        finally
        {
            temporary.Delete(true);
        }
    }

    [WindowsFact]
    [Trait("LongRunning", "true")]
    public async Task Npm_script_timeout_settles_inherited_output_and_the_script_descendant()
    {
        var temporary = Directory.CreateTempSubdirectory("rafter-npm-timeout-");
        await WritePackageAsync(temporary.FullName);
        using var host = new ToolTestHost(temporary.FullName);
        var ready = Path.Combine(temporary.FullName, "ready.txt");
        try
        {
            (await host.Command.RunAsync(["--quiet"], async context =>
            {
                try
                {
                    await context.Npm().RunScript("hold").ScriptArguments(ready).Timeout(TimeSpan.FromSeconds(5))
                        .RunAndCaptureAsync(4096);
                    Assert.Fail("The script must reach its deadline.");
                }
                catch (CapturedProcessTimeoutException)
                {
                    File.Exists(ready).Should().BeTrue("the real script must have started before timeout");
                    var id = int.Parse(await File.ReadAllTextAsync(ready));
                    try
                    {
                        using var process = Process.GetProcessById(id);
                        process.HasExited.Should().BeTrue();
                    }
                    catch (ArgumentException)
                    {
                        // A nonexistent PID confirms that the recorded script process is no longer running.
                    }
                }
            })).Should().Be(0, host.Error.ToString());
        }
        finally
        {
            temporary.Delete(true);
        }
    }

    private static async Task WritePackageAsync(string root)
    {
        await File.WriteAllTextAsync(Path.Combine(root, "package.json"), """
            { "name": "rafter-tool-fixture", "version": "1.0.0", "private": true,
              "scripts": { "echo": "node echo.cjs", "fail": "node -e \"process.exit(7)\"", "hold": "node hold.cjs" } }
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "echo.cjs"), "console.log(JSON.stringify(process.argv.slice(2)));");
        await File.WriteAllTextAsync(Path.Combine(root, "hold.cjs"),
            "require('node:fs').writeFileSync(process.argv[2], String(process.pid)); setInterval(() => {}, 1000);");
    }
}
