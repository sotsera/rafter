# Roots, paths, and filesystem authoring

The first increment was agreed on 2026-10-25 and merged through
[PR #50](https://github.com/sotsera/rafter/pull/50) under [issue #46](https://github.com/sotsera/rafter/issues/46).
The [root guide](../guides/roots-and-paths.md) describes its
public surface. It follows concrete repository-workflow experiments in the original local checkout; publishing
that complete example follows the roots and filesystem PRs in the [delivery plan](example-design-plan.md#next-steps).

## Agreed first increment

Keep paths as ordinary strings for the first increment. Expose the root already captured by the invocation,
provide one explicit resolver, and let command configuration select a different root from a literal or a prepared
string option. The later [filesystem helpers](filesystem-authoring.md) are implemented separately under
[issue #47](https://github.com/sotsera/rafter/issues/47).

Agreed API shape:

```csharp
// On RafterContext:
public string RootDirectory { get; }
public string ResolvePath(string path);

// On Command; each returns the same command for declaration chaining:
public Command RootDirectory(string path);
public Command RootDirectory(ValueOption<string> option);
```

`RootDirectory` describes a path base; it does not change the host process's working directory. The context property
is read-only for the invocation. Child processes continue to default to that root and preserve the existing
`.WorkingDirectory(path)` override.

### Default invocation root

Authoring:

```csharp
var command = Rafter.Command("Inspect repository paths.");

return await command.RunAsync(args, context =>
{
    context.WriteLine(context.RootDirectory);
    context.WriteLine(context.ResolvePath("artifacts/packages"));
});
```

The root remains the working directory captured at `RunAsync` entry. It is not the declaration-time directory or
the script's source directory. Reusing a command captures a new default on each invocation; later CWD changes do
not alter a run's selected root. All its body/target contexts receive that same root.

### Explicit literal root

```csharp
var command = Rafter.Command("Build a repository next to the invocation directory.")
    .RootDirectory("../repository");
```

A relative configured root resolves against the directory captured at invocation entry, once per run. An absolute
configured root is normalized and used directly. Declaring the command does not inspect or create the directory.
Selecting a root leaves the host CWD unchanged.
Normalization removes an optional trailing separator while preserving filesystem roots, so resolving `.` returns
the same root string even when the configured value ends in a separator.

### Root selected by a prepared option

```csharp
var command = Rafter.Command("Build and test this repository.");
var repository = command.Option<string>("root", "Repository root, relative to the invocation directory.")
    .Default(".");

command.RootDirectory(repository);

return await command.RunAsync(args, async context =>
{
    var solution = context.ResolvePath("Rafter.slnx");
    if (!File.Exists(solution))
    {
        throw new InvalidOperationException("The selected root must contain Rafter.slnx.");
    }

    await context.Process("dotnet")
        .Arguments("build", solution)
        .RunAsync();
});
```

The option remains an ordinary script-declared input, with existing CLI/environment/default precedence and help.
There is no new built-in `--root` control. Only a required/defaulted `ValueOption<string>` is accepted as a binding,
so the selector has a value after input preparation; optional strings do not acquire an implicit fallback.

Resolve the selected root once after prepared-input validation succeeds and before any body/target executes. It
uses the original invocation directory even if a validation callback changes the host CWD. Root selection does
not run user code again, and child processes need no repeated `.WorkingDirectory(repository)` setting.

## Resolution and validation contract

| Case | Agreed behavior |
| --- | --- |
| `ResolvePath(".")` | Return the selected absolute root. |
| Relative file or directory path | Normalize against the selected root, including `.` and `..`, independently of later host CWD changes. |
| Fully qualified path | Normalize and use that path; do not prepend the selected root. |
| Null, empty, whitespace-only, or NUL-containing path | Reject. A direct resolver call throws an argument exception within the body. |
| Windows drive-relative or rooted-but-not-fully-qualified path, such as `C:output` or `\output` | Reject rather than depend on an implicit drive/current-directory interpretation. |
| A valid path whose directory/file does not exist | Resolve lexically. Directory existence and creation belong to the script or filesystem operation. |
| Symlink/junction or path outside the root | Resolution does not inspect filesystem links or enforce containment; the root is a base, not an access boundary. |

The first resolver takes one path string, not `params` fragments or a new path type. It returns a string usable by
ordinary .NET APIs and `.Arguments(...)`. The library does not rewrite child argument tokens; those remain relative
to the child's actual working directory unless the author passes an explicitly resolved path.

Command declaration rules:

- Root configuration may be specified once, whether literal or option-bound. A repeated call is a declaration
  error even if the values are identical; initial validation returns `2`, including for help.
- Null/blank/NUL-containing literal configuration, null handles, and handles from another command are declaration
  errors. Root configuration participates in the same invocation snapshot as other declarations.
- Valid help performs no root resolution, directory probing, or user validation. The default `.` stays readable in
  help instead of requiring a declaration-time CWD capture.
- On an executing invocation, malformed selected paths produce a safe configuration/input diagnostic and exit `2`
  before body/target work. Identify the bound option when available, without echoing rejected path text.
- Arbitrary root-selector callbacks, source-directory discovery, and automatic repository discovery are deferred.
  Binding a prepared option covers the actual workflow need without another callback lifecycle.

The existing [preparation and exception rules](example-design-plan.md#invalid-values-and-repeated-callback-configuration)
still govern user input validators. This increment does not give validation callbacks an execution context or new
filesystem facilities. A valid selected path is not a promise that later I/O will succeed.

## Root slice verification and delivery boundaries

The original root slice included:

- 34 portable root cases: literal/prepared/default roots, normalization, argument/declaration validation, safe
  diagnostics, help without root resolution, environment precedence, concurrent invocations, and declaration snapshots.
- Two real-host cases where input validation changes the process CWD after invocation capture. Literal and prepared
  roots still resolve against the captured directory, and an ordinary .NET text write goes to that selected root.
- Four native Windows cases for literal/prepared roots and child-directory overrides. Child argument tokens remain
  unchanged and the host CWD is preserved.
- Fresh package-only project and file-app consumers that select a root through a prepared option, check the public
  root/resolver values, and confirm selecting a nonexistent directory does not create it.

The original root slice excluded the full repository workflow and its success/failure/timeout/pack checks.
Those integration checks now accompany [workflow delivery #52](https://github.com/sotsera/rafter/issues/52);
their results belong to the [workflow guide](../development/repository-workflow.md), rather than this root-only run.
File/directory handles merged separately in [PR #51](https://github.com/sotsera/rafter/pull/51) under #47. Source-directory
discovery, repository discovery, selector callbacks, and native Linux/macOS process ownership remain separate work.

## Decision

Accepted: **ordinary string roots and `ResolvePath` first**, plus literal/prepared-option root declarations.
The context owns the selected path base for one invocation; filesystem I/O remains explicit caller code.

## Independent root-slice verification — 2026-10-25

Verified on Windows x64 with SDK 10.0.402 in the isolated `build/invocation-roots` checkout, based on merged
upstream main `b775ef7`. Build servers are disabled and MSBuild uses one node for this local build. Test commands
use the repository's native Microsoft.Testing.Platform configuration; process/pipe checks run outside the
restricted execution sandbox.

```powershell
dotnet restore Rafter.slnx --disable-parallel --verbosity minimal
dotnet build Rafter.slnx --configuration Release --no-restore --disable-build-servers -m:1 --verbosity minimal
dotnet test --solution Rafter.slnx --configuration Release --no-build -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1 --report-trx --results-directory artifacts/test-results/pr-invocation-roots-full
```

Build: **0 warnings, 0 errors**. Full regression: **Passed: 772, Failed: 0, Skipped: 1** (773 cases: 648 unit,
33 integration, 92 Windows). All 34 root unit cases, two real-host cases, four root process cases, and fresh
package-only consumers passed. The three TRX reports are retained in the test command's results directory.
The skipped graceful-signal case needs an attached console; it is not counted as live signal verification.

Local documentation file/heading checks and four PowerShell parser checks passed; `git diff --check` passed.
The consumer helper still has SHA-256 `FE1C5E40733D64B340272491C9DBBEBFA28FC55CCACB2C502B07F92E68DA4CDC` from
the merged review fixes. No new live editor/terminal, AOT, or native Linux/macOS ownership result is claimed.
Portable Linux results belong to this slice's PR CI. The combined workflow results from the original checkout
are not relabeled as verification of this independently tested branch.

## Root PR delivery record

[PR #50](https://github.com/sotsera/rafter/pull/50) merged on 2026-10-25 as `51c66e9`. Its
[CI run](https://github.com/sotsera/rafter/actions/runs/40211637262), for head `7f10eef`, passed Windows full
**772 tests, 0 failures, 1 console-dependent skip**, portable Linux full **681 tests, 0 failures, 0 skips**,
packaging, and **43 redirected runtime cases**. This predates the separately delivered filesystem helpers.
The complete workflow's remaining acceptance is recorded in the workflow guide under issue #52.
