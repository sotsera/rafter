# Filesystem authoring: first increment

Agreed on 2026-10-25 and merged through [PR #51](https://github.com/sotsera/rafter/pull/51) under
[issue #47](https://github.com/sotsera/rafter/issues/47).
The [filesystem guide](../guides/filesystem.md) describes usage. The
[root/path API](../guides/roots-and-paths.md), merged in [PR #50](https://github.com/sotsera/rafter/pull/50), is the
foundation. The original local repository-workflow experiment supplies the immediate use cases: checking repository
files, creating an evidence directory, selecting tool paths, and writing metadata and a final manifest. The complete
workflow example merged in [PR #53](https://github.com/sotsera/rafter/pull/53), closing #47 and delivery #52;
its integration acceptance and observed results are in the [workflow guide](../development/repository-workflow.md).

## Agreed authoring shape

Use immutable file/directory handles for composing paths and performing a few explicit operations:

```csharp
var solution = context.File("Rafter.slnx");
if (!solution.Exists())
{
    throw new InvalidOperationException("The selected root must contain Rafter.slnx.");
}

var evidence = context.Directory("artifacts", "evidence", "workflows", "repository", runId)
    .EnsureExists();

await evidence.File("package-properties.json")
    .WriteAllTextAsync(metadata, context.CancellationToken);

await context.Process("dotnet")
    .Arguments("build", solution.FullPath,
        "-flp:LogFile=" + evidence.File("build.log").FullPath + ";Verbosity=normal")
    .RunAsync();

// In the existing finally block, after process awaits have settled:
await evidence.File("run.json")
    .WriteAllTextAsync(manifest, CancellationToken.None);
```

The common base path is retained by the directory handle, so selecting a child file needs no repeated `Path.Combine`.
`Directory(...)` and `File(...)` accept multiple path segments and combine them internally. A separate public
`Combine` step is unnecessary in this first surface.
`FullPath` remains an ordinary absolute string for external tools and other .NET libraries. No implicit string
conversion, overloaded path operators, or automatic logging is needed.

## Public surface

| Type | Members in the first increment |
| --- | --- |
| `RafterContext` | `DirectoryPath Directory(params string[] paths)`; `FilePath File(params string[] paths)` |
| `DirectoryPath` | Read-only `string FullPath`; `DirectoryPath Directory(params string[] paths)`; `FilePath File(params string[] paths)`; `bool Exists()`; `DirectoryPath EnsureExists()` |
| `FilePath` | Read-only `string FullPath`; `bool Exists()`; `Task WriteAllTextAsync(string contents, CancellationToken cancellationToken)` |

`DirectoryPath` and `FilePath` are sealed, immutable path handles. They own no open files, enumerators, or other
native resources, so constructing or retaining a handle requires no disposal. They carry the absolute path, not an
execution context or hidden current-directory state. A directory handle can be shared between targets; a later
operation uses the cancellation token explicitly supplied at that call.

These are path/operation helpers, not a new virtual filesystem or an assertion that a file/directory exists.
Creating a file handle for a path that currently names a directory is still lexical; writing to it fails through
the normal filesystem operation. Filesystem identity, equality/comparison policy, and serialization are not added.

## Path behavior

- `context.File(paths)` and `context.Directory(paths)` join segments with `Path.Combine`, then resolve against
  `context.RootDirectory` at handle creation. A directory's `.File(paths)` and `.Directory(paths)` use that
  directory's `FullPath` as the base instead.
- Require a non-null array containing at least one segment. Validate every segment before combining; null, blank,
  NUL-containing, and ambiguous Windows rooted segments are rejected even if a later absolute segment would
  otherwise discard them. Use `.` explicitly when requesting the current base directory.
- `Directory(...)` returns a directory handle; `File(...)` returns a file handle. Neither infers the kind from an
  extension or performs I/O. Selecting and composing the intended path happen in the same call.
- Relative and fully qualified paths follow the existing `ResolvePath` contract: normalize lexically, reject
  null/blank/NUL and ambiguous Windows rooted forms, and allow `.` and `..`. A fully qualified child argument
  selects that absolute path rather than being appended to the directory.
- Directory handles use the same trailing-separator normalization as command roots, preserving filesystem roots.
  Constructing or combining handles never probes, creates, or opens anything and never changes the host CWD.
- The selected root/directory remains a base, not a containment boundary. No symlink/junction traversal or access
  policy is introduced. Ordinary .NET calls continue to follow their own path rules.

## Agreed I/O behavior

| Operation | Behavior |
| --- | --- |
| `.Exists()` | Synchronous probe of the corresponding file/directory, following .NET `File.Exists` / `Directory.Exists` semantics. A false result is not a guarantee that later I/O can succeed. |
| `.EnsureExists()` | Synchronous directory creation, including missing parents. Return the same directory handle for fluent use. Existing directories are preserved; a conflicting file produces the normal I/O failure. |
| `.WriteAllTextAsync(contents, token)` | Create or replace the file with exactly the supplied text, using UTF-8 without a BOM and no added newline. Empty text creates an empty file; null text is rejected before opening/truncating the file. |
| Missing parent for a write | Fail normally. The author explicitly calls `.EnsureExists()` when directory creation is intended; writing does not silently create parents. |
| Existing destination | Truncate/replace its contents, as the name `WriteAllTextAsync` indicates. Append, create-only, and atomic-replace policies remain separate operations to discuss if a use case needs them. |
| Cancellation or I/O failure during a write | Propagate the operation's exception after its stream has settled and closed. The destination may contain partial/truncated data; no transactional or atomic-write promise is made. |

Directory probes/creation remain synchronous instead of acquiring a `Task.Run` wrapper. Path construction performs
no I/O; operation names make probes and mutations visible. A successful existence check does not reserve a path or
remove the usual races with other writers.

### Cancellation and ownership

Require an explicit cancellation token for the initial asynchronous write method. Normal body work passes
`context.CancellationToken`; the final evidence write passes `CancellationToken.None`. There is no omitted-token
overload or hidden invocation-token fallback in this increment. In particular, an explicit `None` must not be
mistaken for "use the context token."

Each returned write task owns its file operation and completes only when that operation's stream has been disposed.
The caller must await it before finishing the body, deleting its directory, or reporting its own cleanup complete.
Do not use a wait timeout that abandons the underlying write. This surface follows ordinary .NET task ownership;
it does **not** add the process runtime's automatic unfinished-operation tracking to filesystem calls.

That ownership choice is agreed for this increment: if equivalent body tracking is wanted for file operations, agree it
as additional execution scope before implementation. Pure path handles should still remain reusable across targets
and independent of an already completed target's context. No ambient current-context lookup is proposed.

The synchronous probes and directory creation have no cancellation-token parameter in this first surface;
they are not advertised as interruptible. The invocation still performs its existing cancellation checks around
body/target execution.

## Workflow cleanup enabled by this increment

- Replace the solution/project `File.Exists` checks with explicit file-handle probes.
- Build the evidence directory through `context.Directory("artifacts", "evidence", "workflows", "repository", runId)`
  followed by `.EnsureExists()`.
- Use file/directory `FullPath` values for build logs, test-result directories, and package destinations.
- Replace metadata/manifest `File.WriteAllTextAsync` calls with the file-handle method, preserving their current
  normal versus noncancelable token choices and the final `try/finally` structure.
- Keep the existing process wrapper and explicit packaging condition. The separate process-authoring and SDK
  environment follow-ups remain on the [development plan](example-design-plan.md#next-steps).

Creating a directory handle for test/package destinations does not create those directories; the respective tool
still creates its own output. Keep only the evidence directory's deliberate `.EnsureExists()` call.

## Considered alternative: root-scoped methods with strings

A smaller alternative would expose a root-scoped filesystem service:

```csharp
// Alternative sketch, also not implemented.
var path = "artifacts/evidence/workflows/repository/" + runId;
var evidence = context.FileSystem.EnsureDirectory(path); // absolute string
await context.FileSystem.WriteAllTextAsync(path + "/run.json", manifest, CancellationToken.None);
```

This avoids public file/directory handle types, but nested operations repeat strings/composition or fall back to
`Path.Combine`. The fluent handle form was selected for the workflow because it gives the evidence directory
one reusable base and makes each operation clear. Both approaches can retain explicit cancellation and ordinary
string interoperability; only the selected handle surface is implemented.

## Acceptance scenarios

- In-process path checks for multi-segment factory/chaining resolution, absolute overrides, empty/null arrays and
  invalid individual segments, immutable values,
  trailing separators, and stability when the host CWD changes. Handle construction must create no paths.
- Real temporary-directory I/O for missing/existing/conflicting directories, file versus directory probes, missing
  parents, empty and Unicode text, BOM/newline behavior, overwrites, and null text leaving existing bytes intact.
- A precanceled write must not create/truncate its destination. Normal awaited writes and fault/cancellation paths
  must leave no owned stream open. Do not claim an in-flight cancellation case verified without exercising one.
- Help/input rejection for the workflow must remain free of filesystem side effects. Its normal/failure/timeout
  paths must retain the same manifests and completed-stage semantics after the cleanup.
- Fresh package-only project/file-app consumers must compile and use the chosen surface. Portable managed I/O
  coverage remains separate from Windows process ownership; new native platform claims require their own runs.

Do not include delete, recursive copy, globbing, stream factories, encoding options, append, JSON serialization,
or generic filesystem cleanup registrations in this first increment. The present workflow does not require them.
The directory/file helper scope remains separate from the earlier root/path increment. Issue #47 tracks delivery;
this document owns its detailed behavior and verification scenarios.

## Delivery boundaries

The original filesystem slice contained the path handles and operations, 16 unit path cases, 13 real-I/O cases,
and extensions to real-host and fresh package-only file-app/project consumers. Its independent results below
exclude the complete workflow and its five consumer cases. The latter now accompany delivery #52; their observed
results are recorded separately in the workflow guide before closing #47's remaining integration acceptance.

No deterministic in-flight cancellation case is claimed. Precancellation and settled success/failure paths are
exercised; actual write/stream lifetime is delegated to the caller-awaited .NET task. SDK-environment isolation,
native Linux/macOS process ownership, AOT, and live editor/terminal verification remain separate work.

## Independent filesystem-slice verification — 2026-10-25

Verified on Windows x64 with SDK 10.0.402 on `feature/filesystem-helpers`, based on merged upstream main
`51c66e9` (PR #50), reusing the clean root-slice checkout. This branch excludes the workflow example and its five
consumer cases. Historical combined results from the original checkout are not relabeled as this slice's results.

```powershell
dotnet build Rafter.slnx --configuration Release --no-restore --disable-build-servers -m:1 -t:Rebuild --verbosity minimal
dotnet test --solution Rafter.slnx --configuration Release --no-build -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1 --report-trx --results-directory artifacts/test-results/pr-filesystem-full-refreshed
```

Rebuild: **0 warnings, 0 errors**. Full regression: **Passed: 801, Failed: 0, Skipped: 1** (802 total: 664 unit,
46 integration, 92 Windows). All 16 path cases, 13 real-I/O cases, extended real-host checks, and fresh package-only
project/file-app consumers passed. The skip needs an attached console; this headless run does not verify that signal.
The three TRX reports are retained in the results directory named above.

The initial local run reported **770 passed, 31 failed, 1 skipped** under `artifacts/test-results/pr-filesystem-full/`.
Manually copied source retained an earlier timestamp, so `PreserveNewest` left the previous root-only `RootPaths.cs`
in consumer fixture output. All 31 failures included the resulting missing-`Combine` compiler error. No shipping
source change was needed: refreshing copied-code timestamps and rebuilding restored the fixtures. All **106** copied
source/project files across both consumer suites were hash-checked against the working tree before the passing run.
Mismatch hashes/timestamps and the refresh check remain under `artifacts/evidence/pr-workflow/` in that checkout.

Local documentation file/heading checks and four PowerShell parser checks passed; `git diff --check` passed.
Precancellation, awaited stream closure, and I/O failure paths are verified; deterministic in-flight cancellation
was not forced. Portable Linux results belong to the PR's own CI run, and no new native platform guarantee follows.

## Filesystem PR delivery record

[PR #51](https://github.com/sotsera/rafter/pull/51) merged on 2026-10-25 as `5684994`. Its
[CI run](https://github.com/sotsera/rafter/actions/runs/40213817028), for head `8b1c72e`, passed Windows full
**801 tests, 0 failures, 1 console-dependent skip**, portable Linux full **710 tests, 0 failures, 0 skips**,
packaging, and **43 redirected runtime cases**. The feature branch avoided a duplicate fork-push evaluation.
The complete workflow's acceptance passed separately and merged in [PR #53](https://github.com/sotsera/rafter/pull/53),
closing #46/#47/#52. The five workflow cases are not included in this filesystem-only CI count.
