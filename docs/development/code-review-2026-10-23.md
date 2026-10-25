# Code review — 2026-10-23

Review baseline: `35a8baa6bc78ae337ed915e8e0768b984eec94d6` (bounded process capture, PR #45).
The existing working-tree change in `examples/environment.cs` was preserved. The initial review made documentation
changes only; the subsequent, explicitly requested fixes are now prepared for delivery under
[issue #48](https://github.com/sotsera/rafter/issues/48). The [main development plan](../design/example-design-plan.md#next-steps)
owns priorities and proposed milestones; this record owns the findings and their evidence.
The [2026-10-25 follow-up](#follow-up-review-and-restoration--2026-10-25) records the missing consumer integration
found during local review and its restoration.

## Scope and assessment

The review covered the shipping runtime and analyzer sources: command/input preparation, validation, target planning,
output, cancellation, process configuration and supervision, capture, deadlines, Windows interop, and authoring
diagnostics. It also inspected package/build/CI configuration, the shared consumer launcher, verification tools,
examples, design contracts, and relevant regression tests. This is a source review with focused probes, not a proof
of correctness or new cross-platform verification. The later fix verification is recorded separately below.

The existing architecture remains appropriate: a small public API, internal execution/ownership components, a separate
analyzer, and three suites with explicit platform/cost boundaries. Important existing tests cover real package and
file-app consumers, descendant cleanup, handle inheritance, blocked output, cancellation/timeout races, capture
boundaries, and immutable declaration snapshots. Additional features should follow a complete consumer workflow.

Four actionable findings are recorded below. R1 and R2 carry forward the first review; R3 and R4 are additional
findings. All four are now **fixed locally, awaiting review**. Proposed features such as explicit roots, conditional execution, sensitive values, and
Linux ownership are tracked as product work in the main plan, not classified here as implementation defects.
The defect descriptions and original observations below refer to the baseline revision; each **Local fix** paragraph
describes the current implementation.

## R1 — Completed process tasks retain captured output (P2, fixed locally)

Source: [BodyProcesses](../../src/Sotsera.Rafter/Internal/BodyProcesses.cs), particularly `Run` and `FinishAsync`, and
[CapturedStream](../../src/Sotsera.Rafter/CapturedProcessResult.cs).

Every started operation is added to `_operations`. Completed tasks are never removed. `FinishAsync` filters the list
to find unfinished work but does not clear the retained entries. A completed capture task retains its result, which
owns both stream text and chunks. Repeated awaited captures in one long-running body therefore retain previous output
even when the caller has discarded the results. The limit bounds each capture, not the accumulated retained captures.
A retained context or builder can also keep the tracker alive after the body ends.

**Observed:** a component probe ran three real, awaited `dotnet --version` captures with a 1,024-character per-stream
limit and 15-second process timeout, discarded the caller's result references, and forced collection. The tracker's
list still contained all three completed tasks and their 30 captured stdout characters. This confirms retention; it
is not a memory benchmark or a measurement of large-workload impact.

**Fix direction:** track outstanding operations without retaining settled results unnecessarily. Keep registration,
completion/removal, and body closure race-safe. Preserve existing semantics for abandoned work, caught failures,
sticky settlement failures, and typed capture tasks; releasing a task must not mean abandoning live process work.

**Regression expectation:** repeated captures whose results the caller releases become collectible while the body
continues; concurrent unfinished work remains owned and is joined on body completion. Do not use exact allocated-byte
thresholds as the correctness contract.

**Local fix:** the tracker holds a set of operations and removes each settled task under the same lock used for
registration and closure. Body completion takes ownership of its unfinished-operation snapshot and clears the set.
The new real-process regression releases four capture results and confirms their stream objects become collectible
while the body and reusable builder remain alive. Existing abandoned-operation and settlement tests remain applicable.

## R2 — One CLI conversion error hides later independent errors (P3, fixed locally)

Source: [InvocationArguments.Parse](../../src/Sotsera.Rafter/Internal/InvocationArguments.cs), the conversion loop's
`IsPresent(parsed) && diagnostics.Count == 0` condition.

The condition uses the global diagnostic count. After the first enum/duration conversion fails, later non-Boolean
options are not converted. Reporting therefore depends on declaration order, and a user must fix inputs in multiple
rounds even when their independently supplied tokens could all be checked.

**Observed:** a command with two duration options, invoked with `--first=bad --second=bad --plain`, returned exit `2`
and reported only `--first`. The body did not run. The same guard applies to named enums.

**Fix direction:** distinguish structural parser failures from per-option conversion failures. Convert independently
valid option tokens and aggregate their diagnostics, without calling `GetValue` on structurally invalid parser results.
Preserve the existing gates around environment lookup, help, user validation callbacks, and execution.

**Regression expectation:** two invalid duration/enum values report both option identities in one invocation;
declaration-order reversal does not hide either error. Rejected values remain undisclosed and callbacks do not run.

**Local fix:** structural errors mark their own options invalid. Other present options convert independently through
their option result, allowing safe diagnostics to accumulate. Environment fallback, help, validation, and execution
retain their existing gates. Ten regression cases cover duration/enum combinations and order, plus malformed,
missing, repeated, and unknown input alongside an independently invalid duration.

## R3 — Shared-terminal destinations do not share line-position state (P2, fixed locally)

Source: [InvocationOutput](../../src/Sotsera.Rafter/Internal/InvocationOutput.cs), construction of the two destinations,
and [RendererDestination](../../src/Sotsera.Rafter/Internal/RendererDestination.cs), `_lineStart`, `Literal`, and `Layout`.

When stdout and stderr paint the same terminal, the destinations share a semaphore but each keeps its own
`_lineStart`. A partial stdout write changes only stdout's state. A subsequent framework diagnostic on stderr still
believes it is at the start of a line and appends its heading directly to the child text. The reverse stream direction
can similarly produce prefixes in the middle of a physical terminal line. Atomic writes alone do not fix this.

**Observed:** a controlled probe used two production `RendererDestination` instances with a shared writer and gate,
matching the shared-terminal coordination arrangement. After stdout wrote `partial child stdout` without a newline,
stderr rendered the framework heading `Process failed`. The resulting plain transcript was:

```text
partial child stdoutProcess failed
```

This verifies the renderer-state defect with a controlled common sink. It is not a new live-terminal matrix run.

**Fix direction:** coordinate physical line state for a shared terminal while preserving independent redirected
destinations and raw child-stream contents. Define prefix/line-break behavior when the emitting stream changes;
do not solve this by making a redirected stdout pipe block unrelated stderr output.

**Regression expectation:** partial stdout followed by a framework stderr message starts the heading on its own line.
Cover both stream directions, rich/plain output, and mixed redirection. Retain the existing write-atomicity checks.

**Local fix:** `DestinationState` owns the gate and line position. Invocation stdout/stderr share that state only when
both paint the terminal. Framework layout starts a fresh line after either stream's partial text; literal writes
continue the physical line without inserting another prefix. Eight new cases cover both directions, rich/plain
rendering, and every redirection combination; the existing atomic-write cases remain.

## R4 — Verification deadlines omit output drain (P2, fixed locally)

Sources: [IsolatedConsumer.ExecuteInAsync](../../tests/Shared/IsolatedConsumer.cs),
[Verify-RuntimeTerminal](../../tools/verification/Verify-RuntimeTerminal.ps1),
[Verify-RendererTerminal](../../tools/evaluations/Verify-RendererTerminal.ps1), and
[Measure-Renderer](../../tools/evaluations/Measure-Renderer.ps1).

The launchers begin asynchronous reads, apply a timeout to root-process exit, and subsequently await the reads without
a deadline. If the root exits while a descendant still holds a redirected pipe open, root exit succeeds but EOF never
arrives. The nominal two-minute consumer timeout (or tool-specific exit timeout) no longer applies. Timeout cleanup
also contains unbounded exit/read joins. The suite can consequently stall until the outer CI job timeout, and local
tool runs have no equivalent outer bound.

**Initial evidence:** direct control-flow inspection. The later fix adds a native hanging-descendant reproduction.
This concerns test/tool
supervision, not the shipping `ProcessOperation` supervisor, which already models process/output settlement separately.

**Fix direction:** supervise exit and output completion together, with an explicit cleanup policy and retained partial
diagnostics. Cancellation of a read is not proof that descendants terminated. Avoid merely adding a timeout that
abandons the reader tasks or deletes a consumer workspace while its processes are still using it.

**Regression expectation:** a controlled parent exits after starting a descendant that retains stdout/stderr. The
harness reports the drain failure within its intended bound, settles its readers, and verifies its owned cleanup.
Also cover root timeout, cancellation, and separate stdout/stderr completion.

**Local fix:** a shared, test/tool-only [supervisor](../../tools/shared/VerificationProcess.cs) applies one deadline to
root exit and both stream readers. On failure it attempts tree termination, waits up to five seconds for root exit,
then cancels and joins its owned reads/wait. The thrown exception carries partial streams and explicit descendant
cleanup uncertainty. `IsolatedConsumer` retains its workspace and diagnostics; the PowerShell wrapper records a
unique failure JSON in the tool's evidence directory. No shipping dependency or public API is added.

Six native regression cases cover exact partial-line output, large independent stdout/stderr output, an exited
parent whose descendant retains the pipes, live-root timeout/cancellation, and retained consumer workspace/evidence.
The four deliberate deadline/cancellation cases use the existing long-running profile. Each descendant reproduction
owns and separately terminates its recorded probe PID before deleting its own test files. The helper's tree-kill
attempt is best effort: it does not claim native descendant containment, especially after the root has already exited.

## Release and maintenance follow-ups

These are improvements, separate from the four correctness findings:

- Enable and package XML API documentation; the runtime source has summaries, but the project does not currently
  enable documentation-file generation. Verify the package asset and a fresh consumer's documentation lookup.
- Establish preview versioning, release notes, and public API compatibility checks before relying on published APIs.
- Keep the README/current-status entry concise and link historical milestone counts as history. Avoid repeating
  detailed acceptance lists across this plan, feature guides, and future GitHub issues.
- Preserve the current runtime/analyzer split. Source-file grouping can improve navigation without introducing
  additional production layers or changing native ownership boundaries.

## Initial review verification and retained evidence

- `dotnet build src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --no-restore` succeeded with
  **0 warnings and 0 errors**, rebuilding the runtime and analyzer used by the probes.
- PowerShell 7.6.5 / .NET 10.0.11 ran the focused component/public-API probes described under R1–R3. Their source and
  JSON result are retained locally under `artifacts/evidence/reviews/20261023-development-review/` as
  `inspect-runtime.ps1` and `runtime-results.json`.
- The first native capture attempt was denied access to its local named pipe inside the execution sandbox. Its
  failure is retained in `capture-probe-failure.txt`. The same probe succeeded outside that sandbox. This environmental
  restriction is not classified as a product defect.
- R4 was supported by source inspection at this stage; its native regression was added with the local fix.
- The three updated/new documents passed a local file-link and referenced-heading check: **177 targets, 0 problems**.
  `git diff --check` found no whitespace errors. External URLs were not revalidated by this documentation check.
- No full .NET test suite, Linux/macOS run, AOT check, or live editor/terminal verification was performed here.
  The prior delivered CI results remain linked from the main plan and are not relabeled as new test results.

The original probes above describe the baseline defect, not the fixed working tree. Keep these records as historical
evidence. Local fix results follow; delivery references should be added after local review and committing.

## Local fix verification — 2026-10-23

These results apply to the uncommitted working tree based on `35a8baa`, on Windows x64 with SDK 10.0.402 and
PowerShell 7.6.5. Build commands use a single MSBuild node with build servers disabled in this local environment.
Test commands use native MTP syntax. Local process/pipe tests ran outside the restricted execution sandbox.

```powershell
dotnet build Rafter.slnx --configuration Release --no-restore --disable-build-servers -m:1 --verbosity minimal
dotnet test --solution Rafter.slnx --configuration Release --no-build -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1 --report-trx --results-directory artifacts/test-results/review-fixes-full
```

Build: **0 warnings, 0 errors**. Full regression: **Passed: 732, Failed: 0, Skipped: 1** (733 cases).
The skipped graceful-signal test requires an attached console; this headless run does not verify that scenario.
The run includes real file-app/project/package consumers, captured-result collection, and inherited-pipe failures.

The final rebuild also includes stronger input assertions and long-running classification for the four deliberate
supervisor deadline/cancellation cases. The consumer helper was restored after an unexplained local file reversion,
then checked in the final diff. Ordinary selection and all six supervisor cases were verified again:

```powershell
dotnet test --solution Rafter.slnx --configuration Release --no-build --minimum-expected-tests 1 --max-parallel-test-modules 1 --report-trx --results-directory artifacts/test-results/review-fixes-final-ordinary
dotnet test --project tests/Rafter.WindowsTests/Rafter.WindowsTests.csproj --configuration Release --no-build -p:IncludeLongRunningTests=true --filter-class Rafter.WindowsTests.Processes.VerificationProcessTests --minimum-expected-tests 6 --report-trx --results-directory artifacts/test-results/review-fixes-final-supervisor
```

- Final ordinary profile: **Passed: 670, Failed: 0, Skipped: 1** (614 unit, 1 integration, 56 Windows cases).
- Final supervisor class: **Passed: 6, Failed: 0, Skipped: 0**, including workspace retention after drain timeout.
- Initial focused unit run: **Passed: 614, Failed: 0, Skipped: 0**, retained under
  `artifacts/test-results/review-fixes-unit/`. Each later run's TRX reports are in the directory named by its command.

```powershell
dotnet build tests/testassets/consumers/runtime/project/Consumer.csproj --configuration Release --no-restore --disable-build-servers -m:1 --verbosity minimal
./tools/verification/Verify-RuntimeTerminal.ps1 -RedirectedOnly
```

Runtime consumer build: **0 warnings/errors**. Redirected runtime matrix: **43 passed**, with records under
`artifacts/evidence/verification/runtime-terminal/20261023T170747838Z-0af56ac523974e78bf872b45f03481a5/`.

The local `artifacts/evidence/reviews/20261023-review-fixes/verify-wrapper.ps1` probe also verified the PowerShell
wrapper's timeout exception and failure JSON containing partial stdout and cleanup uncertainty under strict mode.
Its successful evidence directory is `wrapper-1325fea837fa40c583a7bba19172d847/` beside the probe. The initial
`wrapper-b7b62d01d34f4eefbf350c0a589e5f73/` attempt is retained too: its probe invocation omitted the required
runtime argument and correctly recorded the resulting child error; correcting that invocation resolved the check.

`./tools/evaluations/Measure-Renderer.ps1 -Samples 1` completed for all six variant/host combinations. It verifies
the updated launcher's build, capture, and JSON paths; it is not a performance benchmark. Its raw measurement record
is under `artifacts/evidence/evaluations/renderer-measurements/20261023T171003312Z-fe76e4789e034e57a284ef7f06d4549b/`.

The final documentation check found **209 valid local file/heading targets, 0 problems**. All four changed PowerShell
scripts parsed successfully, and `git diff --check` found no whitespace errors. External links were not revalidated.

No new Linux/macOS execution, AOT, live editor, or attached-terminal matrix was run for these fixes. Controlled
shared-terminal tests establish line-state behavior; the redirected matrix does not substitute for live terminal
verification. Descendant cleanup uncertainty remains explicit in test/tool failure results. Nothing is staged or
committed; review the local changes before preparing delivery.

## Follow-up review and restoration — 2026-10-25

The uncommitted-change review found that `tests/Shared/IsolatedConsumer.cs` had reverted to its baseline contents.
The new workspace-retention test referenced the missing `ExecutionTimeout` property, causing a fresh solution build
to fail with CS0117. The old launcher also retained unbounded output-drain waits and unconditional workspace deletion.
The earlier passing results above did not describe that reverted working tree.

Restored the complete launcher integration with `VerificationProcess`: the default two-minute deadline covers root
exit and stream drain, test cancellation is forwarded, and uncertain cleanup retains the workspace and a unique
partial-output diagnostic file. Successful launches keep the existing result/logging behavior and normal disposal.
The existing six native supervisor regressions include the inherited-pipe timeout and workspace-retention scenarios.

The restored solution builds with **0 warnings and 0 errors** using:

```powershell
dotnet build Rafter.slnx --configuration Release --no-restore --disable-build-servers -m:1 --verbosity minimal
dotnet test --solution Rafter.slnx --configuration Release --no-build -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1 --report-trx --results-directory artifacts/test-results/review-fix-20261025-full
```

Fresh full regression: **Passed: 732, Failed: 0, Skipped: 1** (733 cases; exit 0). UnitTests reports 614 passed,
IntegrationTests 31 passed, and WindowsTests 87 passed with the expected console-capability skip. All six native
supervisor regressions passed, including consumer workspace/partial-output retention after inherited-pipe timeout.
The three TRX reports are retained in `artifacts/test-results/review-fix-20261025-full/`.

The restored helper was checked in the working-tree diff after execution and retains the verified SHA-256
`FE1C5E40733D64B340272491C9DBBEBFA28FC55CCACB2C502B07F92E68DA4CDC`. The existing development plan is updated
in place with the current result and the next workflow increment. Local file/heading checks pass for 210 targets;
the four changed PowerShell scripts parse, and `git diff --check` passes. No new attached-console, Linux/macOS,
AOT, editor, or terminal-matrix verification was performed on this date. Changes remain unstaged and uncommitted.

## Independent PR slice verification — 2026-10-25

Delivery resumed under [issue #48](https://github.com/sotsera/rafter/issues/48) on `build/review-fixes`, based on
`35a8baa`. A separate checkout contains only R1–R4, their regressions, and related documentation. The later root,
filesystem, and repository-workflow increments remain in the original checkout and were not part of this run.
The uncommitted-status statements above describe their earlier local snapshots; this section records the slice
prepared for commit and PR review.

```powershell
dotnet restore Rafter.slnx --disable-parallel --verbosity minimal
dotnet build Rafter.slnx --configuration Release --no-restore --disable-build-servers -m:1 --verbosity minimal
dotnet test --solution Rafter.slnx --configuration Release --no-build -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1 --report-trx --results-directory artifacts/test-results/pr-review-fixes-full
dotnet build tests/testassets/consumers/runtime/project/Consumer.csproj --configuration Release --disable-build-servers -m:1 --verbosity minimal
./tools/verification/Verify-RuntimeTerminal.ps1 -RedirectedOnly
```

Both builds completed with **0 warnings/errors**. Full regression: **Passed: 732, Failed: 0, Skipped: 1** (733 cases:
614 unit, 31 integration, 88 Windows including the console-capability skip). All six native verification-supervisor
regressions and fresh package/file-app/project consumers passed. The three TRX reports are retained in the directory
named by the test command in the isolated checkout.

The redirected runtime matrix passed **43 cases**, with evidence under
`artifacts/evidence/verification/runtime-terminal/20261025T155914387Z-99a2a7dcad704f999211a7f119bece5f/`.
Local file/heading checks and four PowerShell parser checks passed, as did `git diff --check`. The restored
`IsolatedConsumer.cs` hash still matches the value recorded above. This local run does not establish attached-console,
live editor/terminal, native Linux/macOS ownership, or AOT behavior. Portable Linux results belong to the PR's CI run.
