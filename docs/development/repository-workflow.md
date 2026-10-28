# Repository workflow

[repository-workflow.cs](../../examples/repository-workflow.cs) exercises the existing public API in one maintained
file-based app. It inspects bounded JSON from MSBuild, builds the repository, runs its tests, and optionally packs
the local runtime. This is a local example for the [development plan](../design/example-design-plan.md#next-steps);
it introduces no new runtime API or CI entry point. Owned child processes require Windows.

This is a maintained baseline for authoring review. Its root/filesystem APIs merged through PRs #50/#51; revisit
the remaining process setup as authoring improves. The [workflow cleanup follow-up](../design/example-design-plan.md#workflow-cleanup-follow-up)
tracks that work. The remaining boilerplate should not become the final recommended script style by default.
The [roots/filesystem design](../design/roots-and-filesystem.md) records the agreed root-access increment now used
by this example. The [filesystem guide](../guides/filesystem.md) and
[design](../design/filesystem-authoring.md) describe its now implemented directory/file helpers, explicit I/O, and
caller-awaited writes. The normal and final-write cancellation choices are preserved.

Delivered in [PR #53](https://github.com/sotsera/rafter/pull/53), closing #46/#47/#52. The
[post-merge main evaluation](https://github.com/sotsera/rafter/actions/runs/40221671404), for `d8e27a2`, passed
Windows full **806 cases, 0 failures, 1 console-dependent skip**, Linux portable full **710 cases, 0 failures,
0 skips**, packaging, and **43 redirected runtime checks**. Earlier verification sections below retain the
results of their original revisions, including the corrected PR output assertion.

## Run

From the repository root with the pinned .NET SDK:

```powershell
dotnet run --file examples/repository-workflow.cs -- --help
dotnet run --file examples/repository-workflow.cs -- --plain
dotnet run --file examples/repository-workflow.cs -- --pack --full-tests --configuration Release
```

For a launch from another directory, supply the script's absolute path and `--root`:

```powershell
dotnet run --file E:/Ghidello/Git/rafter.v2/rafter/examples/repository-workflow.cs -- --root E:/Ghidello/Git/rafter.v2/rafter --pack --plain
```

`--root` is processed after `dotnet run` has selected the outer file-app SDK. The current local #62 cleanup opts its
children into [UseWorkingDirectorySdk](../guides/executables-and-dotnet.md#selecting-the-sdk-for-the-child-directory),
which removes documented inherited SDK/MSBuild overrides and lets the selected child directory govern SDK lookup.
It does not choose the outer host SDK or guarantee that another host SDK can build the file app. Building in the
checkout and launching the printed DLL path with `dotnet <built-workflow.dll> --root <repository>` remains the
SDK-safe host workaround. The older observed preview-SDK failure is preserved below; the current installed preview
does not reproduce its natural environment injection, while controlled contamination does reproduce and verify
the child-side fix.

Relative roots resolve against the invocation directory, never the script directory. `command.RootDirectory(root)`
binds the prepared input; `context.RootDirectory` and `context.ResolvePath(...)` then serve ordinary file paths and
default child directories consistently. The script verifies that the chosen root contains `Rafter.slnx` and the
runtime project. The host CWD is unchanged. Build the file app once before using `dotnet run --no-build`.

| Input | Default and behavior |
| --- | --- |
| `--root` | `.`; resolved against the directory captured at `RunAsync` entry. |
| `--configuration` | `Release`; accepts the named `Debug` and `Release` enum values. |
| `--full-tests` | `false`; when true, enables the repository's `IncludeLongRunningTests` profile. |
| `--pack` | `false`; creates a package only after inspection, build, and tests succeed. |
| `--timeout` | `10m` per child process, including owned process/output settlement; must be positive. Expiry initiates shutdown and still waits for owned work to settle. |

Inspection runs `dotnet msbuild` with `-getProperty:PackageId,Version,TargetFramework` and captures up to 16,384 UTF-16
code units per stream. It parses the actual JSON result; captured text is not replayed automatically. Build uses the
selected configuration and disables build servers. Test uses native Microsoft.Testing.Platform solution syntax,
`--no-build`, a nonempty selection requirement, one concurrent test module, and TRX reporting. The full-profile
property is added only when requested. Packaging reuses the successful build without restoring or building again.

`--quiet` follows the runtime contract: framework progress is reduced, while child output and the workflow's result
lines remain visible. `--plain` removes framework styling. Help and invalid prepared inputs launch no child processes
and create no evidence directory.

## Evidence and failure behavior

Each invocation creates a unique UTC timestamp/GUID directory under
`artifacts/evidence/workflows/repository/<run>/` and prints its absolute path:

| File | Meaning |
| --- | --- |
| `package-properties.json` | The bounded, parsed MSBuild property result. |
| `build.log` | MSBuild's normal-verbosity build log. |
| `tests/` | Native MTP TRX results, including reported capability skips. |
| `pack.log` and `packages/` | Created only for a requested pack stage reached after successful tests. Use this printed package directory as an isolated local feed. |
| `run.json` | Root, configuration, requested profile/package behavior, timestamps, package metadata, completed stages, and success state. |

A failed inspection/build/test or expired process deadline throws through the existing process API, preventing later
steps. `run.json` is written in `finally` with a noncancelable evidence write after the process await settles; it
records only completed stages and defaults to unsuccessful. An omitted pack stage is announced explicitly. The
workflow creates no temporary workspace and deletes no evidence or packages. Retention follows the
[generated-file policy](generated-files.md).

Cancellation relies on the existing Rafter process ownership and command cancellation contract. A new live-console
cancellation scenario is not implied by the workflow checks. A successful test command can contain explicit
capability skips; inspect its TRX reports before treating a scenario as covered.

## Authoring observations

- **The root/path and filesystem APIs merged through #50/#51.** The command binds `--root` and the invocation resolves
  it once; the script no longer captures CWD or repeats `.WorkingDirectory(repository)` on each child. The later
  filesystem cleanup replaces manual path composition/probes, directory creation, and text writes with the agreed
  multi-segment helpers. Reusable executable configuration and typed .NET commands merged in #61.
- Ordinary C# sequencing and `if (pack)` express this workflow clearly. There is no demonstrated need here for a
  new target condition API; later examples must establish skip/dependency behavior before adding one.
- The local `Dotnet` function was replaced by immutable `context.Dotnet()` settings for
  [issue #60](https://github.com/sotsera/rafter/issues/60). Build and pack use typed commands; metadata and native-MTP
  test options use grouped raw arguments. See [executables and .NET](../guides/executables-and-dotnet.md).
- The local #62 increment opts into `.UseWorkingDirectorySdk()` without changing generic or default .NET inheritance.
  Controlled SDK 11 targets under MSBuild 10 reproduced MSB4216/MSB4027; the policy restored real metadata/resource
  builds under 10.0.403. Natural outside launch on the current preview also passed without injected overrides.
- Process awaits own shutdown/output delivery. `try/finally` owns the final evidence write. The script does not
  establish a shared-resource cleanup API or the behavior of multiple simultaneous cleanup failures.

## SDK policy verification, 2026-10-27

The current local #62 increment opts the workflow into `.UseWorkingDirectorySdk()`. A controlled inherited SDK 11
`MSBuildSDKsPath` loaded SDK 11 targets under MSBuild 10 and reproduced MSB4216/MSB4027 in a real resource project.
The opt-in restored SDK 10.0.403 metadata/resource builds from both inside and outside the checkout. The natural
outside launch on the currently installed preview had no inspected SDK overrides and succeeded without the policy;
this is distinct from the older preview observation below, not evidence of continuing natural injection.

The final full local regression passed **866 tests, 0 failures, 1 console-dependent skip** (867 total), including all
five workflow success/failure/timeout/optional-pack scenarios. All **43 redirected runtime cases** passed. SDK probe
logs and failed attempts remain in `artifacts/evidence/sdk-tools/`; final TRX reports are under
`artifacts/test-results/sdk-tools-full-final/`. New Git/frontend file-app examples and fresh tool/package consumers
also passed. See [the tool verification record](../guides/git-and-npm.md#local-verification-2026-10-27).
These are local results for the reviewed PR increment; hosted CI remains pending. Linux CI, attached-console, and
live-editor checks were not run locally for this increment.

## Historical local verification before delivery slicing

The current executable/.NET authoring cleanup is verified separately for local issue #60 work:
all five workflow cases passed within the **830 passed / 0 failed / 1 skipped** full regression. Typed build and pack
replace the local helper, while metadata and native-MTP test arguments remain explicit chunks. See the
[current verification record](../guides/executables-and-dotnet.md#local-verification-2026-10-27).
The older observations below retain their original source revisions and counts.

The [Windows workflow tests](../../tests/Rafter.WindowsTests/Consumers/RepositoryWorkflowTests.cs) use the actual
file app in isolated workspaces. A small real solution containing the runtime and a sample test avoids recursively
running the outer repository's workflow tests. Scenarios cover default and explicit relative roots, configuration,
ordinary/full-profile arguments, optional packaging, help/invalid inputs without side effects, failed build/test
gates, and timeout evidence. Local results are not delivery or cross-platform evidence.

Observed on 2026-10-25, Windows x64, SDK 10.0.402, in the uncommitted working tree based on `35a8baa`:

```powershell
dotnet build tests/Rafter.WindowsTests/Rafter.WindowsTests.csproj --configuration Release --no-restore --disable-build-servers -m:1 --verbosity minimal
dotnet test --project tests/Rafter.WindowsTests/Rafter.WindowsTests.csproj --configuration Release --no-build -p:IncludeLongRunningTests=true --filter-class Rafter.WindowsTests.Consumers.RepositoryWorkflowTests --minimum-expected-tests 5 --report-trx --results-directory artifacts/test-results/repository-workflow-final
```

Workflow consumer cases: **Passed: 5, Failed: 0, Skipped: 0**. The initial five-case run reported three passes and two
failures because the standalone sample test fixture lacked the TRX extension requested by the workflow. Adding the
already centrally versioned reporter fixed that fixture; both the initial and final TRX reports are retained under
`artifacts/test-results/repository-workflow-initial/` and `artifacts/test-results/repository-workflow-final/`.
Failure checks assert the deliberate compiler/assertion failure, so runner startup failures cannot satisfy them.

The initial file-app build used an unsupported `-m:1` argument; removing it allowed file-app compilation. That
sandboxed restore reported NU1900 because vulnerability metadata was unreachable. The subsequent real workflow
build restored successfully with **0 warnings and 0 errors**; no audit suppression was introduced.

From `E:/Ghidello/Git/rafter.v2` (the parent of the checkout), this actual repository run used a relative explicit root:

```powershell
dotnet run --file E:/Ghidello/Git/rafter.v2/rafter/examples/repository-workflow.cs --configuration Release -- --root rafter --pack --plain
```

It completed inspection, build, the ordinary tests (**Passed: 670, Failed: 0, Skipped: 1**), and packaging. The skip
requires an attached console. Evidence is retained under
`artifacts/evidence/workflows/repository/20261025T133953846Z-a1f93bcf809d489aa9764f325209a162/`, including the three
TRX reports and `Sotsera.Rafter.0.1.0-local.nupkg` in its own package directory. `run.json` records all four completed
stages and success. A package-only file app in that run's `package-consumer/`, using source mapping to this feed and
a fresh `.packages` directory, ran `dotnet run --file consumer.cs -- --quiet` successfully and printed
`Package consumer: workflow.` It used no production project reference.

These initial prototype results predate the root/path increment. Its later full workflow and outside-launch results
are recorded below. The earlier [733-case full run](code-review-2026-10-23.md#follow-up-review-and-restoration--2026-10-25)
predates the five added workflow cases. No new live-terminal/cancellation, Linux/macOS, editor, or AOT checks were
performed. Prototype verification does not complete the broader cleanup or platform milestones.
The documentation check covers 276 local file/heading targets with zero problems; the four maintained PowerShell
tools parse, and `git diff --check` passes. All changes remain unstaged and uncommitted for local review.

### Verification after root/path cleanup

The earlier combined root/path experiment (before delivery slicing) is retained below. The [root/path record](../design/roots-and-filesystem.md#independent-root-slice-verification--2026-10-25) owns the final
implementation checks. The actual checkout workflow with default `--root=.` and `--full-tests --pack --plain`
reports **Passed: 777, Failed: 0, Skipped: 1** and a successful local package; the build reports 0 warnings/errors.
Its run is `20261025T143104645Z-fd46351528a44617a8db20da03ca237d` under the workflow evidence directory.

The first outside-checkout retry used `dotnet run --file ... --configuration Release --no-build -- --root rafter/ --plain`
from the checkout's parent. That launch directory selected installed SDK `11.0.100-rc.2.26504.105` rather than the
repository's 10.0.402. The file-app host passed its MSBuild SDK paths into children, which then failed with
MSB4216/MSB4027 despite their correctly selected root. Its failed manifest and build log are retained under
`20261025T143538099Z-69e282b0d42042df94421ae4e9f23392/`; only inspection completed, and no tests or pack ran.

Building under the pinned SDK and directly launching that built DLL from `E:/Ghidello/Git/rafter.v2` succeeded:

```powershell
# From the checkout:
dotnet build examples/repository-workflow.cs --configuration Release --no-restore --disable-build-servers --verbosity minimal
# From its parent, using the DLL printed by that build in this environment:
dotnet C:/Users/Alessandro/AppData/Local/Temp/dotnet/runfile/repository-workflow-ce2216305ee453ab24b355fc74ab3f1a435a507fb0aa2a74b5e7d367edd8de18/bin/release/repository-workflow.dll --root rafter/ --plain
```

The explicit root with a trailing separator resolves to the same canonical root; the ordinary run reports
**Passed: 710, Failed: 0, Skipped: 1**, with packaging explicitly omitted. Records are under
`20261025T143759843Z-0af8df46d71b4b739fb3b887c3947a49/`. SDK-environment isolation is a process-authoring follow-up,
not a capability added by root selection. Prefer the pinned SDK or the verified built-DLL launch in this environment.

### Verification after filesystem cleanup

The earlier combined filesystem/workflow experiment is retained below. The [filesystem record](../design/filesystem-authoring.md#independent-filesystem-slice-verification--2026-10-25) owns the new
implementation checks and exact commands. The actual checkout workflow with `--full-tests --pack --plain` reports
**Passed: 806, Failed: 0, Skipped: 1** (807 cases), a build with 0 warnings/errors, and a successful local package.
Its evidence directory is `20261025T152408225Z-12afb323346e482aad3d825234eb3759` under the workflow evidence root.

The successful manifest, metadata file, and tool paths now use the agreed multi-segment file/directory helpers.
Fresh package consumers exercised those helpers, and the existing five workflow scenarios passed after the cleanup.
The earlier 778-case full and outside-checkout runs above predate this increment. SDK-environment isolation remains
an open process-authoring follow-up; no new outside-checkout, Linux/macOS, AOT, or live-console claim is made here.

## Independent final-delivery verification — 2026-10-25

Prepared under [issue #52](https://github.com/sotsera/rafter/issues/52) on `feature/repository-workflow`, based on
merged upstream main `5684994` (PR #51), in `E:/Ghidello/Git/rafter.v2/invocation-roots`. No shipping runtime or CI
YAML changed. Rebuild: **0 warnings/errors**. All **230** copied fixture files were hash-checked against the source.

```powershell
dotnet build Rafter.slnx --configuration Release --no-restore --disable-build-servers -m:1 -t:Rebuild --verbosity minimal
dotnet build examples/repository-workflow.cs --configuration Release --no-restore --disable-build-servers --verbosity minimal
dotnet run --file examples/repository-workflow.cs --configuration Release --no-build -- --full-tests --pack --plain
```

The actual full workflow completed inspection, build, tests, packaging, and its successful final manifest.
Full regression: **Passed: 806, Failed: 0, Skipped: 1** (807 cases: 664 unit, 46 integration, 97 Windows), including
all **five** workflow consumers. The skip requires an attached console. Build: **0 warnings/errors**.
Evidence run `20261025T172425538Z-9004045cb14a46b38b5e67885ac1344b` contains metadata, build/pack logs, three TRX
reports, successful `run.json`, and `packages/Sotsera.Rafter.0.1.0-local.nupkg` under the workflow evidence root.

From its parent (`E:/Ghidello/Git/rafter.v2`), the verified SDK-safe outside launch was:

```powershell
dotnet C:/Users/Alessandro/AppData/Local/Temp/dotnet/runfile/repository-workflow-78bf4e454c5050499dc187246eed9a27d8d10a0321e8d3b0ba41c09e86ae10e7/bin/release/repository-workflow.dll --root invocation-roots/ --plain
```

It completed inspection/build/ordinary tests, explicitly skipped packaging, and wrote a successful manifest:
**Passed: 739, Failed: 0, Skipped: 1** (740 cases). Evidence run:
`20261025T173215503Z-85ff44dc74ad43caaa76244a4de2fc3c`. The DLL path was printed by this environment's build;
other checkouts should use their own printed path. Root selection still does not fix SDK-environment inheritance.

The initial full attempt (`20261025T171657732Z-69cb046c20ab4863abc225de1c1b741f`) reported **805 passed, 1 failed,
1 skipped**: an existing process API file-app consumer reached its two-second deadline before emitting expected
startup output. All five new workflow cases passed. The failed real workflow stopped before packaging and retained
an unsuccessful manifest with only `inspect`/`build` completed. Both existing process consumer cases then passed in
isolation under `artifacts/test-results/pr-workflow-timeout-consumer/`.

The new build-heavy workflow cases now use the existing nonparallel native collection to avoid overlapping
short-deadline consumer probes; timeout semantics and assertions were not weakened. The subsequent complete run
above passed. A restricted-context file-app build also raised MSB4018 in SDK asset resolution; the same command
succeeded in the approved unrestricted context without a source/SDK change. The exact SDK-side cause was not proven.

Local documentation file/heading and PowerShell parser checks passed, as did staged whitespace checks. These
observations complete the deferred workflow integration for #46/#47, subject to PR CI and user review. No live
workflow cancellation, attached-console/editor, AOT, or new native Linux/macOS ownership result is claimed.

### PR CI output correction

The first [PR #53 CI run](https://github.com/sotsera/rafter/actions/runs/40217216344) passed Linux portable coverage
(710 cases) but reported **804 passed, 2 failed, 1 skipped** in the Windows full suite. Both workflow success cases
completed their actual work; their assertions incorrectly rejected ANSI escapes anywhere in captured stdout.
GitHub's child test runner emits color when redirected. Rafter's `--plain` controls framework output, while raw child
streams retain their original content.

The assertions now require the three workflow-owned result lines to be present and plain, preserving the child-stream
contract. With process-scoped `CI=true` and `GITHUB_ACTIONS=true`, the focused workflow class passed **5/5** cases,
including colored runner output. The Windows test project build had **0 warnings/errors**. The original failed CI log
is retained in `artifacts/evidence/pr-workflow/windows-ci-initial.log`; focused TRX results are in
`artifacts/test-results/pr-workflow-ci-output/`. Full CI must verify the corrected PR revision.
