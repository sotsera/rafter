# Captured process output

Implemented for [issue #44](https://github.com/sotsera/rafter/issues/44), following the
[agreed design](../design/process-capture.md). Owned process execution currently requires Windows.
The [runnable example](../../examples/process-capture.cs) captures a child's JSON, parses it in script code,
and optionally displays the recorded mixed transcript.

```csharp
var result = await context.Executable("tool")
    .Arguments("--json")
    .Timeout(TimeSpan.FromSeconds(10))
    .RunAndCaptureAsync(maxCharactersPerStream: 4096);

using var json = JsonDocument.Parse(result.StandardOutput.Text);
```

`RunAndCaptureAsync(int maxCharactersPerStream, bool echo = false,
Func<CapturedProcessResult, ProcessFailureAction>? onFailure = null)` executes the same builder settings as
`RunAsync()`, returning a sealed `CapturedProcessResult : ProcessResult` after owned processes, readers, echo,
and any rejected-exit callback have settled. Each call executes anew with its own configuration snapshot and capture.
RAF003 diagnoses clearly discarded capture tasks; the runtime also owns unfinished operations until the body settles.

## Streams and order

`StandardOutput` and `StandardError` are immutable, non-null `CapturedStream` objects. Each has `Text` and
`IReadOnlyList<CapturedChunk> Chunks`. Empty streams have empty text/collections. Text preserves the child's decoded
whitespace, line endings, and escape sequences without adding Rafter's prefixes or styling.

Each nonempty chunk has `long Order` and `string Text`. One counter starts at one across both streams for this capture.
Combine and sort their chunks by `Order` to inspect the order Rafter recorded them. It does not reconstruct the child's
original cross-stream write chronology, lines, or write-call boundaries. Optional `echo: true` uses this same recorded
sequence for live child output; capturing is silent by default. Framework lifecycle messages still follow verbosity.

## Explicit limits

The positive limit counts UTF-16 code units, matching `Text.Length`, independently for each stream. There is no library
default and unused capacity is not transferred between streams. A nonpositive argument throws synchronously before
process admission or launch, leaving the builder unchanged. An input validator can reject invalid user options earlier.

Exact-limit output is valid. The first additional decoded text that cannot fit freezes retention and new echo for both
streams, initiates owned shutdown, and drains/discards remaining pipe data. Already queued echo is still joined.
Retained text and chunk metadata are bounded; no separate public chunk limit is needed because chunks are nonempty.
The character budget is not an exact allocation-byte quota, and constructing `Text` also requires bounded storage.

Retention never splits a valid surrogate pair, including at reader/chunk boundaries. With a limit of two, `A😀` retains
`A` and overflows: the two-unit scalar cannot fit. A retained prefix can therefore be shorter than the limit.
Grapheme clusters and multi-scalar emoji sequences are not guaranteed to remain whole.

After clean settlement, overflow throws `ProcessCaptureLimitException`. It exposes `Executable`, `Stream`
(`ProcessOutputStream.StandardOutput` / `StandardError`), `Limit`, non-null `PartialStandardOutput` /
`PartialStandardError`, and nullable `ObservedExitCode`. It never returns a truncated normal result. A caught clean
overflow can recover; an unhandled one produces invocation exit `1`. Catching cannot clear cancellation or prior failure.

## Rejected exits

Use the capture-specific callback to inspect streams without a cast:

```csharp
var result = await context.Executable("tool")
    .RunAndCaptureAsync(4096, onFailure: captured =>
    {
        context.WriteLine(captured.StandardError.Text);
        return ProcessFailureAction.Continue;
    });
```

The selected callback runs exactly once for a rejected exit, after complete capture and settlement. `Continue` and
`Fail` return the captured result with `Succeeded = false`; `Fail` also records sticky invocation failure. `Throw`
and no handler throw existing `ProcessFailureException`, whose `Result` preserves the `CapturedProcessResult` object.
Catching that shared exception uses a type pattern to access streams. Explicitly accepted nonzero codes succeed.

Builder `OnFailure` remains available through its `ProcessResult` parameter. Combining it with a non-null capture-call
callback throws synchronously before launch. Omitted/null `onFailure` uses the builder handler or default `Throw`.
Capture callbacks do not persist on the builder. Callback exceptions/invalid actions retain the original rejection
and callback error. No rejected-exit callback runs for launch, capture, timeout, cancellation, or output failures.

## Timeout, cancellation, and settlement failures

Clean captured timeouts throw sealed `CapturedProcessTimeoutException : ProcessTimeoutException`. It retains the
ordinary timeout metadata and adds non-null `PartialStandardOutput` / `PartialStandardError`, `CaptureLimit`, and nullable
`OverflowedStream`. Existing timeout catches still work. Catching a clean timeout permits recovery under existing rules.

Invocation cancellation throws sealed `CapturedProcessCanceledException : OperationCanceledException`, with the same
partial-stream/limit/overflow properties plus `Executable` and nullable `ObservedExitCode`. It carries the exact invocation
cancellation token. A validated capture call cancelled before launch exposes empty streams. Catching permits inspecting
data but cannot reopen launch admission or restore success; otherwise clean invocation cancellation returns `130`.

Capture/echo continues through shutdown and draining unless overflow has already frozen it. Partial text may include
shutdown diagnostics; it does not promise the interrupted child flushed all intended output. Snapshots are immutable
after consumers join. Cancellation requested before timeout/overflow commitment wins; otherwise the first committed
timeout or overflow remains primary. Secondary overflow enforces the same bound and becomes metadata. No later event
restarts or extends the existing shutdown/drain budgets; timeout monitoring disarms after confirmed owned completion.
Cancellation notices write outside the stopping-cause lock, so a blocked destination cannot prevent supervision or
cause commitment. The invocation still joins that notice before final output settlement.

**Termination unknown**, **Output incomplete**, and independent failures keep their existing sticky precedence.
Available bounded counts and stopping context remain in framework diagnostics; public access to partial text through
settlement-failure exceptions is deferred. These paths never masquerade as clean recoverable capture stops.

## Output

Normal/verbose overflow emits a yellow `Capture limit exceeded | tool` notice at commitment. Unhandled settled overflow
emits one red failure heading, stream/limit, retained partial counts, and a known exit code. Unhandled captured timeout
adds counts and secondary-overflow context to the ordinary timeout block. Caught clean overflow/timeout has no final
red block. Normal cancellation retains the global notice/summary; per-process captured cancellation counts are verbose
only. Accepted capture counts are verbose only, while emitted rejected-exit blocks include complete capture counts.

Quiet suppresses progress and keeps failures. Plain/redirected output retains information without styling;
`NO_COLOR` removes color. No mode automatically replays raw captured text, including verbose. Scripts and explicit
echo control whether that text is displayed. See [output contracts](../design/process-capture.md#agreed-capture-failure-presentation).

## Verification and scope

[Buffer checks](../../tests/Rafter.UnitTests/Processes/ProcessCaptureBufferTests.cs) cover text, immutable chunks, Unicode,
bounds, ordering, and controlled cause commitment. [Supervision checks](../../tests/Rafter.UnitTests/Processes/ProcessCaptureTests.cs)
cover callbacks, partial shutdown output, cause precedence, blocked/failed echo, and settlement failures.
[Output checks](../../tests/Rafter.UnitTests/Processes/ProcessCaptureOutputTests.cs) cover counts, quiet/verbose policy,
and avoiding automatic replay. Configuration and analyzer checks remain in their existing unit classes.

[Windows tests](../../tests/Rafter.WindowsTests/Processes/ProcessCaptureTests.cs) exercise real child streams, descendants,
concurrent isolation, repeated calls, snapshots, abandonment, and native timeout/cancellation.
[Consumer checks](../../tests/Rafter.WindowsTests/Consumers/ProcessConsumerTests.cs) run the example;
[API consumers](../../tests/Rafter.WindowsTests/Consumers/ProcessApiConsumerTests.cs) exercise file-app/project hosts.
Exact-commit CI results belong to the delivery PR. Local verification results are recorded there after execution.

Observed locally on Windows x64 / SDK 10.0.402, 2026-10-23, before the delivery commit:

- Full solution: **Passed: 704, Failed: 0, Skipped: 1**. The headless skip requires an attached console.
- After the additional unknown-exit and uncertainty checks, ordinary solution: **Passed: 648, Failed: 0, Skipped: 1**;
  focused failed-termination case: **Passed: 1, Failed: 0, Skipped: 0**. These runs are not added together as one run.
- Final capture unit selection: **Passed: 41, Failed: 0, Skipped: 0**. The separate pseudoterminal graceful-signal
  invocation reports **Passed: 1, Failed: 0, Skipped: 0**, covering the headless capability skip separately.
- A subsequent blocked-cancellation-notice regression reproduced the prior lock defect. After the correction, the
  full unit suite reports **Passed: 596, Failed: 0, Skipped: 0**, with no build warnings. Before/fixed reports are retained
  separately under `artifacts/process-capture/blocked-notice-before-fix/` and `final-unit-fixed/`.
- Release build and local package succeed with **0 warnings/errors**. All **43 redirected** and **118 terminal/mixed**
  output checks pass. Raw TRX/package evidence is retained under `artifacts/process-capture/`; output records use the
  repository's `artifacts/evidence/verification/runtime-terminal/` policy.

The full command was `dotnet test --solution Rafter.slnx -c Release -p:IncludeLongRunningTests=true
--minimum-expected-tests 1 --max-parallel-test-modules 1 --report-trx --results-directory artifacts/process-capture/full`.
The ordinary command omits the inclusion property and uses `artifacts/process-capture/ordinary`.
Focused runs use the corresponding project and native xUnit `--filter-class` / `--filter-method` flags.

Single-stream/binary capture, source-level merging, live parsing callbacks, disk spooling, custom encodings, Linux-native
ownership, macOS, and native AOT remain outside this increment. Portable tests do not establish native process support.
