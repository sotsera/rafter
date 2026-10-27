# Executable configuration and typed .NET commands

Implemented locally for [issue #60](https://github.com/sotsera/rafter/issues/60), following the
[process authoring design](../design/example-design-plan.md#process-authoring-draft-for-review).
The change replaces `context.Process(...)` with `context.Executable(...)`; this is a source-breaking rename.
Owned execution still requires Windows. Portable consumers can configure commands without launching them.

## Generic executable configuration

```csharp
var dotnet = context.Executable("dotnet")
    .Environment("DOTNET_NOLOGO", "1")
    .Timeout(TimeSpan.FromMinutes(5));

await dotnet
    .Arguments("build", solution.FullPath)
    .Arguments("--configuration", "Release")
    .Arguments("--disable-build-servers", "-m:1")
    .RunAsync();

var version = await dotnet.Arguments("--version")
    .RunAndCaptureAsync(maxCharactersPerStream: 1024);
```

`Executable` is immutable. Environment, directory, and timeout methods return new executable configurations; retain
the returned value. Every `Arguments(params string[])` call on an executable creates a fresh `ProcessCommand`.
Subsequent argument calls on that command append tokens to that command only. An option and its value can share a
call; arguments remain separate tokens and do not pass through a shell. Arrays are copied and argument order is
preserved, including empty tokens. Null/NUL tokens are rejected before launch.

A single-use chain is identical, without storing the executable first:

```csharp
await context.Executable("tool")
    .Arguments("--name", "a value with spaces")
    .RunAsync();
```

`Executable.RunAsync()` and `RunAndCaptureAsync(...)` each create a fresh zero-argument command. `ProcessCommand`
remains a mutable invocation builder: retaining it and appending arguments changes that command, not an executable
template. Each execution snapshots its current configuration; builders are not single-use.

## Defaults, overrides, and lifetime

Executable defaults support `Environment`, `RemoveEnvironment`, `WorkingDirectory`, and `Timeout`. A child command
can override each default once. Duplicate explicit writes within the executable chain or child chain remain errors;
environment names compare case-insensitively on Windows. The executable, siblings, and parent environment are unchanged.

```csharp
var tool = context.Executable("tool")
    .Timeout(TimeSpan.FromMinutes(5))
    .Environment("MODE", "default");

await tool.Arguments("probe")
    .Timeout(TimeSpan.FromSeconds(30))
    .Environment("MODE", "probe")
    .RunAsync();

// This command retains the five-minute timeout and MODE=default.
await tool.Arguments("work").RunAsync();
```

The environment is inherited at launch, then executable edits and child edits are applied in order.
`RemoveEnvironment(params string[] names)` removes a variable entirely; `Environment(name, "")` keeps an empty value.
Set/remove conflicts within one layer are errors. Removal validates the entire batch before changing a child command.
No automatic .NET SDK sanitization is performed. The
[outside-launch SDK caveat](../development/repository-workflow.md#verification-after-rootpath-cleanup) remains open;
environment removal alone does not establish a complete SDK-selection policy.

The default directory is the invocation root. Both executable and child relative directories resolve against that
root; neither changes host CWD. Timeout defaults apply per process and do not replace invocation cancellation.
Failure handlers and accepted exit codes belong to the child command. Capture bounds and echo are execution choices.

Executable configuration and commands belong to the creating body. They cannot launch after it finishes or rebind
to another target. Configuration starts no process. Explicitly awaited concurrent children are supported. Shutdown,
stream drain, callbacks, and settlement use the existing [owned process pipeline](process-runtime.md).

## Typed .NET commands

`context.Dotnet()` returns immutable `DotnetExecutable` configuration over the same generic API. It uses ordinary
environment inheritance. Every typed command selection creates an independent invocation:

```csharp
var dotnet = context.Dotnet()
    .Environment("DOTNET_CLI_USE_MSBUILD_SERVER", "0")
    .Timeout(TimeSpan.FromMinutes(5));

await dotnet.Build(solution.FullPath)
    .Configuration("Release")
    .NoRestore()
    .DisableBuildServers()
    .MaxCpuCount(1)
    .RunAsync();

await dotnet.Publish(project.FullPath)
    .Configuration("Release")
    .Output(output.FullPath)
    .RunAsync();

await dotnet.Pack(project.FullPath)
    .Configuration("Release")
    .NoBuild()
    .Output(packages.FullPath)
    .RunAsync();
```

Paths may be omitted to let the CLI use the child working directory. Configuration names remain strings, so custom
configurations work. The first surface follows the documented
[build](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build),
[publish](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish), and
[pack](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack) command options:

| Surface | Methods |
| --- | --- |
| All three commands | `Configuration`, `Output`, `NoRestore`, `DisableBuildServers`, and raw `Arguments`. |
| Build | `MaxCpuCount(int)` requires a positive count. |
| Publish and pack | `NoBuild`, which the CLI also interprets as disabling implicit restore. |
| Child execution settings | `Timeout`, `WorkingDirectory`, `Environment`, `RemoveEnvironment`, `AcceptExitCodes`, and `OnFailure`. |
| Execution | `RunAsync` and bounded `RunAndCaptureAsync`. |

Typed CLI option calls append arguments in order. They do not deduplicate flags, reconcile conflicting raw options,
or validate installed SDK capabilities. Repeated process settings retain the generic single-assignment rules.
Relative CLI path arguments retain the tool's working-directory semantics; use `FilePath.FullPath` or
`DirectoryPath.FullPath` when an invocation-root path is intended.

Raw arguments remain available on both the reusable .NET executable and typed commands:

```csharp
var metadata = await dotnet
    .Arguments("msbuild", project.FullPath)
    .Arguments("-nologo", "-getProperty:PackageId,Version,TargetFramework")
    .RunAndCaptureAsync(maxCharactersPerStream: 16 * 1024);

await dotnet.Build(solution.FullPath)
    .Configuration("Release")
    .Arguments("-property:ContinuousIntegrationBuild=true")
    .RunAsync();
```

The [repository workflow](../../examples/repository-workflow.cs) uses typed build/pack and grouped raw metadata/test
arguments. Typed test-runner policy and Git/npm helpers remain future increments. Tool adapters compose public
generic commands; there is no separate launcher or SDK package dependency. RAF003 recognizes discarded execution
tasks on generic commands, executable zero-argument methods, and typed .NET commands.

## Local verification, 2026-10-27

Observed on Windows x64 with SDK 10.0.403 (permitted by the existing latest-patch policy), in the uncommitted #60
branch based on merged PR #59. No SDK pin, package dependency, test-project wiring, CI workflow, or timeout policy
was changed.

| Check | Observed outcome |
| --- | --- |
| Final Release solution build | Passed, 0 warnings and 0 errors. |
| Full regression | 830 passed, 0 failed, 1 console-dependent skip; 831 total. |
| Suite breakdown | Unit 680/0/0; integration 46/0/0; Windows 104/0/1 (passed/failed/skipped). |
| Migrated repository workflow | All five scenarios passed: success/default and explicit roots, optional pack, deliberate build/test failures, timeout, and final evidence. |
| New executable and typed .NET cases | All eight native cases passed, including independent/concurrent arguments, environment absence versus empty values, directory overrides, zero-argument/stale-body execution, typed token vectors, and real build/publish/pack. |
| Final fresh-package check | Passed for both project and file-app consumers after adding typed API construction and typed discarded-task diagnostics to the packaged surface check. |
| Redirected runtime verification | All 43 cases passed. |

Raw logs are retained under `artifacts/evidence/executable-dotnet/`; TRX results are under
`artifacts/test-results/executable-dotnet-full-unsandboxed/` and
`artifacts/test-results/executable-dotnet-package-final/`. The initial sandbox run admitted no tests because named-pipe
IPC was denied; the unrestricted run above passed. A restricted-network runtime-consumer restore failed its NuGet
audit check; the unrestricted retry succeeded, and a fresh root restore cleared the cached audit warnings before
the final clean build. The failed attempts remain in the evidence directory.

These are local results, not PR CI or new platform-support claims. Linux CI, attached-console graceful signalling,
live-editor completion, AOT, and an SDK sanitization policy were not verified by this increment. The initial full
run covered the final runtime code; only fresh-package consumer test source was extended afterward, rebuilt, and
verified in the focused final package run.
