# Typed Git and npm commands

The local increments for [Git #63](https://github.com/sotsera/rafter/issues/63) and
[npm #64](https://github.com/sotsera/rafter/issues/64) compose the existing
[executable API](executables-and-dotnet.md). Execution currently requires Windows process ownership. There are no
implicit installs, remote Git operations, new dependencies, or alternative process launchers.

## Git

```csharp
var git = context.Git()
    .WorkingDirectory(repository.FullPath)
    .Timeout(TimeSpan.FromMinutes(1));

var status = await git.Status()
    .Short()
    .Branch()
    .RunAndCaptureAsync(maxCharactersPerStream: 4096);

var log = await git.Log()
    .MaxCount(5)
    .Oneline()
    .RunAndCaptureAsync(maxCharactersPerStream: 4096);
```

Each command selection creates an independent invocation. `GitExecutable` settings are immutable and follow the
generic executable's environment/directory/timeout rules. `Status().Porcelain()` selects stable version-one porcelain
output. `Log().MaxCount(int)` requires a positive count. Rafter retains Git's output; there is no built-in status/log
parser. These methods map to the documented [status](https://git-scm.com/docs/git-status) and
[log](https://git-scm.com/docs/git-log) options.

Raw tokens remain available for paths and options outside the typed subset:

```csharp
var changed = await git.Status()
    .Porcelain()
    .Arguments("--", "src")
    .RunAndCaptureAsync(maxCharactersPerStream: 4096);
```

`git.Arguments(...)` starts a fresh untyped command. The [repository-status example](../../examples/repository-status.cs)
shows status and recent commits from a selected root. A repository with no commits makes `git log` fail normally;
use the existing rejected-exit policy when that case needs handling. Git's own configuration is inherited normally.

## npm

```csharp
var npm = context.Npm()
    .WorkingDirectory(frontend.FullPath)
    .Timeout(TimeSpan.FromMinutes(5));

await npm.Ci().NoFund().RunAsync();

await npm.RunScript("build")
    .ScriptArguments("--mode", "production")
    .ScriptArguments("--verbose")
    .RunAsync();
```

`Install()` uses the package manifest; `Ci()` requires an existing compatible lockfile. Both support `NoAudit`,
`NoFund`, `IgnoreScripts`, and raw `Arguments`. These options are explicit; normal npm audit/lifecycle behavior
remains the default. A command starts when `RunAsync()` or `RunAndCaptureAsync(...)` is called; await the returned
task to observe completion. See npm's
[install](https://docs.npmjs.com/cli/v11/commands/npm-install/) and
[ci](https://docs.npmjs.com/cli/v11/commands/npm-ci/) contracts.

`RunScript(name)` requires a nonblank name not beginning with `-`. A missing script fails by default. `IfPresent()`
selects npm's optional-script behavior and must precede `ScriptArguments`. Each script-argument call accepts one,
two, or many tokens, inserts the `--` boundary only on the first call, and preserves order. Caller arrays are copied;
invalid argument batches leave the command unchanged. Raw arguments before the boundary are npm options; after it,
they are script arguments. This follows [npm run](https://docs.npmjs.com/cli/v11/commands/npm-run/).

On Windows, the adapter runs **Node with npm-cli.js directly**, avoiding the outer npm.cmd/cmd.exe argument-parsing
layer. npm itself still runs package scripts through its configured script shell. Its quoting, lifecycle, environment
changes, and exit behavior remain npm's responsibility. Rafter does not claim arbitrary strings bypass that script
shell. Ordinary argument tokens including spaces, Unicode, and tested shell punctuation are verified with the installed
npm version; generic raw process token guarantees stop at the invoked tool's own argument handling.

`Npm()` finds the first Node executable on PATH, then the paired `node_modules/npm/bin/npm-cli.js`. On Unix layouts,
it can also resolve a sibling `npm` symlink directly to npm-cli.js. It does not run npm.cmd, parse a manager's shell
wrapper, or search a later Node version silently when the first installation lacks npm. Node/npm are caller-installed
tools. Unsupported managers can supply explicit paths:

```csharp
var npm = context.Npm(nodePath, npmCliPath)
    .WorkingDirectory(frontend.FullPath);
```

Explicit relative paths resolve against the invocation root; a bare Node executable name uses the generic launcher.
The CLI script path is always absolute, so it cannot be parsed as a Node option. Discovery uses parent PATH at
configuration time; a child PATH edit does not retroactively choose another Node installation. Automatic discovery
may report missing files while configuring a tool, but it launches no process. Explicit paths are not probed until
execution. Windows is the verified execution platform; portable discovery tests do not establish Unix ownership.

The [frontend example](../../examples/frontend.cs) requires a package root, runs its named script, and performs
`npm ci` only when `--install` is supplied. A lockfile is required for that opt-in stage.

## Shared execution and extension point

Typed commands expose `Arguments`, `Environment`, `RemoveEnvironment`, `WorkingDirectory`, `Timeout`,
`AcceptExitCodes`, `OnFailure`, `RunAsync`, and bounded `RunAndCaptureAsync` through `ToolCommand<TCommand>`.
Tool-specific options retain each concrete fluent return type. The protected constructor accepts a `ProcessCommand`,
so a custom adapter can compose the public generic API without implementing launch or cleanup.

All operations retain the originating body's ownership, explicit awaiting, child override rules, capture limits,
failure handling, timeout/cancellation, stream drain, and settlement. Saving a typed command retains a mutable single
invocation; save its executable to reuse defaults. RAF003 also recognizes inherited execution methods in custom
`ToolCommand<TCommand>` adapters.

## Local verification, 2026-10-27

Observed on Windows x64 with SDK 10.0.403, Git 2.56.0.windows.2, Node 24.21.0, and npm 11.19.0. All changes remain
on the #62/#63/#64 delivery branch based on merged #61; these are local results, not PR CI. The local review on
2026-10-28 found no blocking code issues. Hosted checks and user review remain delivery checkpoints.

| Check | Observed outcome |
| --- | --- |
| Release solution and runtime-consumer builds | Passed, 0 warnings and 0 errors. |
| Full regression | 866 passed, 0 failed, 1 console-dependent skip; 867 total. |
| Suite breakdown | Unit 696/0/0; integration 49/0/0; Windows 121/0/1 (passed/failed/skipped). |
| SDK isolation | Controlled mixed-SDK metadata/resource failure reproduced; opt-in metadata/resource builds passed inside and outside. Stable invalid-path regression and all six removal/override cases passed. |
| Git/npm native checks | All 15 focused SDK/Git/npm cases passed in the full run, including exact argument vectors, real repository status/log, offline install/CI, script forwarding/failure, and script timeout settlement. |
| Consumers | Fresh file-app/project tool execution and isolated fresh-package API/analyzer checks passed. All five repository-workflow scenarios passed. |
| Runnable examples | Repository status/log and frontend script file apps passed their smoke runs. |
| Redirected output | All 43 runtime cases passed. |

Tests use disposable repositories and dependency-free packages; fixture commits have explicit fixture identity and
signing disabled for those fixtures only. npm fixture installs use offline mode. The real npm timeout case verifies
that the script process is gone after capture/ownership settlement. The first focused run exposed read-only Git
object files during fixture cleanup; normalizing attributes in that newly allocated fixture fixed cleanup without
weakening assertions. A first full attempt was interrupted after real consumer compilation exposed missing explicit
cancellation-token arguments in the new fixture. The corrected fixture was separately compiled/smoke-tested before
the complete passing run. Failed/interrupted logs remain under `artifacts/evidence/sdk-tools/`.

Final full TRX reports are under `artifacts/test-results/sdk-tools-full-final/`. No dependencies, SDK pins, CI workflow,
test projects, solution filters, existing timeout thresholds, or process ownership guarantees were changed. Linux
CI, live-editor/attached-console checks, macOS, and AOT were not run for this increment.
