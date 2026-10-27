# Captured process output

Agreed design, 2026-10-23. The execution name `RunAndCaptureAsync(...)`, typed failure-callback direction, and
per-stream `CapturedStream` objects with `Text` / `Chunks` are agreed. Chunks use one shared observation-order counter
for both streams within a process capture. Explicit positive UTF-16 limits and the overflow/settlement/recovery rules
below are agreed, including surrogate-pair-safe retention, capture-limit exception properties, and overflow/timeout/
cancellation arbitration. Immediate validation, single-handler selection, and capture-specific timeout/cancellation
exceptions with non-null partial streams and capture-failure presentation are also agreed. The final review accepts
result inheritance, public data types, a single public capture budget, and deferral of public settlement-failure data access.
Implemented for [issue #44](https://github.com/sotsera/rafter/issues/44); see the
[capture guide](../guides/process-capture.md) for usage, verification scope, and limitations.
The [new example](../../examples/process-capture.cs) runs against the Windows process runtime. The preserved
[reference example](../../reference/examples/process-capture.cs) supplies scenarios, not an API requirement.

## Agreed first slice

Let a script read a tool's text output, such as JSON, a Git revision, or a generated path, while preserving the existing
owned-process lifetime and outcome contracts.

- Opt in to capture for one process operation. Retain stdout and stderr separately as decoded text, preserving
  whitespace, partial lines, and line endings. Do not add Rafter's presentation prefixes or ANSI styling to the stored
  text. The child's own text, including any escape sequences it writes, remains its output.
- Capture silently by default, with an explicit setting to also echo both streams live through the existing output
  handling. Silent capture concerns child output; framework lifecycle/failure messages still follow ordinary verbosity.
- Require an explicit positive limit per stream, counting UTF-16 code units to match `Text.Length`. There is no
  library-wide capture default; the example's 1024 is a script choice. Bound retained output and chunk metadata while
  reading. Exact-limit output is valid; additional text exceeding the limit commits overflow.
- On overflow, freeze capture and new child echo, shut down the affected owned scope, and drain/discard remaining
  pipe data until settlement, including completion of already queued echo. Throw `ProcessCaptureLimitException` after
  clean settlement, with bounded partial streams available, rather than returning a truncated normal result. A caught
  settled limit failure can recover; an unhandled one returns `1`. Preserve whole surrogate pairs when retaining text;
  the limit exception exposes the agreed frozen partial streams and metadata below.
- Return captured streams only after owned descendants and output handling have settled. A fully captured accepted
  exit supplies ordinary exit metadata and non-null `CapturedStream` objects. A stream with no output has empty `Text`
  and an empty `Chunks` collection.
- Keep `OnFailure` limited to rejected exit codes. Capture overflow, timeout, cancellation, and output failures must
  not masquerade as accepted exits or enter that callback as ordinary rejected-exit results.
- Preserve existing timeout/cancellation arbitration, invocation failure precedence, and ownership obligations. Capture
  or echo failure is not permission to abandon readers or descendants. Each concurrent process owns its own buffers.
- Do not automatically print captured contents in framework diagnostics. Scripts may explicitly inspect or display
  them. Bounded partial streams are agreed for capture overflow, timeout, and cancellation, through the exception
  shapes below. Capture-failure presentation is agreed separately below.

Begin with the existing Windows process runtime and decoded text. Single-stream selection, binary capture, callbacks
for live parsing, source-level stream merging, disk spooling, custom encodings, and Linux-native ownership are separate
topics. The agreed stream/chunk shape below retains observation order. Rafter does not parse JSON itself; the example
uses `System.Text.Json` in the script.

## Agreed execution name

Use `RunAndCaptureAsync(...)` to execute with captured output, alongside the existing `RunAsync()` for streamed output:

```csharp
var result = await context.Executable("tool")
    .Arguments("--json")
    .Timeout(TimeSpan.FromSeconds(10))
    .RunAndCaptureAsync(maxCharactersPerStream: 1024, echo: false);

using var document = JsonDocument.Parse(result.StandardOutput.Text);
```

Both execution methods use the same `ProcessCommand` configuration methods for arguments, timeout, environment,
accepted exits, and failure handling. The combined method starts and awaits its owned operation. There is no separate
capture builder or capture configuration step. The limit is explicitly required and positive, with immediate
prelaunch validation and the single-handler rules agreed below. There is no library default for the capture limit;
the result type relationship and public type names are agreed below.

## Agreed result type

Use `RunAndCaptureAsync(...)` returning `Task<CapturedProcessResult>`, while `RunAsync()` retains
`Task<ProcessResult>`. The execution method determines the static result type.

Agreed immutable result surface:

| Member | Type | Meaning |
| --- | --- | --- |
| `Succeeded` | `bool` | Existing accepted-exit semantics, after successful capture and settlement. |
| `ExitCode` | `int` | The observed root exit code. |
| `StandardOutput` | `CapturedStream` | Captured stdout, with its text and ordered chunks; non-null. |
| `StandardError` | `CapturedStream` | Captured stderr, with its text and ordered chunks; non-null. |

The stream shape and containing result type's inheritance are agreed. This allows
`result.StandardOutput.Text` directly, without `result.Capture!`, null suppression, or a separate lookup.
Make `CapturedProcessResult` a sealed subtype of `ProcessResult`, sharing the existing immutable exit
metadata and adding the two immutable captured streams. Remove `sealed` from `ProcessResult`; its internal
constructor continues to keep result construction under the runtime's control. This public-type change is agreed.

The same captured result flows through `ProcessFailureException.Result` without changing its `ProcessResult`
signature or discarding the captured text. Normally returned capture results and the preferred capture callback would
be statically typed; catching the shared process exception uses a type pattern to inspect captured data.
No separate captured rejected-exit exception is needed.

The reviewed `GetCapturedOutput()` alternative is not selected: capture availability is represented by the result type.

## Agreed captured stream shape

Each stdout/stderr capture is a `CapturedStream`, with:

| Member | Type | Meaning |
| --- | --- | --- |
| `Text` | `string` | Non-null concatenation of this stream's retained chunk text, with original whitespace/line endings. |
| `Chunks` | Read-only chunk collection | This stream's immutable chunks, in their recorded order; empty when no text was retained. |

Each chunk has an integer `Order` and non-null `Text`. One shared counter increments across both streams for that
process operation, starting at one; there are no duplicate order numbers in the combined capture. Each concurrent
process capture has its own counter. A stream's sequence can have gaps because the other stream received those numbers:

```text
StandardOutput.Chunks: (1, "Building...\n"), (3, "Done.\n")
StandardError.Chunks:  (2, "Warning...\n")
```

Combine the two collections and sort by `Order` to obtain the recorded mixed transcript. Stream identity follows from
the collection containing the chunk. A separate combined `OutputChunks` result property is not part of this agreed
shape. Use immutable `CapturedChunk` objects with `long Order` and `string Text`, exposed through
`IReadOnlyList<CapturedChunk>`. The underlying collection must also prevent mutation.

`Text` and `Chunks` describe the same retained content, not independently collected copies that can disagree. The
implementation must account for chunk metadata and any concatenated string allocation when bounding storage.

## Agreed rejected-exit handler behavior

Apply rejected-exit handling only after complete capture and owned-scope settlement. Keep the existing control-flow
and invocation-outcome rules, retaining both streams even when the exit code is rejected. The exact containing result
type relationship and shared exception access are agreed above:

| Outcome or action | Capture call outcome | Body and invocation |
| --- | --- | --- |
| Accepted exit | Return `CapturedProcessResult`, `Succeeded = true`. | Body continues; invocation can succeed. |
| `Continue` | Return the captured result, `Succeeded = false`. | Body continues; this rejection alone does not fail the invocation. |
| `Fail` | Return the captured result, `Succeeded = false`. | Body continues; failure is sticky and later targets stop. |
| `Throw` or no handler | Throw `ProcessFailureException` containing the captured result. | Unwind unless caught; unhandled rejection produces exit `1`. |

Handling a rejected exit does not turn it into a successful process result. An explicitly accepted nonzero exit does
count as success under the existing accepted-code policy. Captured text is available for accepted and rejected exits
when capture itself completed successfully; a stream with no output supplies empty `Text` and `Chunks`.

Use the typed `onFailure` callback supplied directly to `RunAndCaptureAsync`, receiving the complete captured result
after a rejected exit and settlement. The agreed callback direction and selection rules use
`Func<CapturedProcessResult, ProcessFailureAction>`:

```csharp
var result = await context.Executable("tool")
    .Arguments("--json")
    .RunAndCaptureAsync(maxCharactersPerStream: 1024, onFailure: captured =>
    {
        context.WriteLine(captured.StandardError.Text);
        return ProcessFailureAction.Fail;
    });
```

Recommend preserving the same result instance through the callback and any normal return or rejected-exit exception.
There is no need for a type pattern inside this callback. It remains a rejected-exit handler, not a per-chunk notification
or a timeout/overflow handler. The existing streamed `OnFailure` API retains its current signature.

A caught `ProcessFailureException` would expose the same object through its existing `Result` property. Pattern
matching retrieves the captured type; no new exception type or automatic diagnostic dump is proposed:

```csharp
catch (ProcessFailureException exception)
{
    if (exception.Result is CapturedProcessResult captured)
    {
        context.WriteLine(captured.StandardError.Text);
    }
}
```

The script explicitly chooses whether to display that text. Framework diagnostics retain exit metadata without
automatically printing captured contents. Catching can recover from `Throw` under the existing exception boundary;
it cannot erase a prior `Fail`. Output/capture failure, overflow, timeout, and cancellation remain separate paths,
whose partial-data and precedence rules are agreed below; public settlement-failure data access is deferred.

Reject an existing builder `OnFailure` combined with a non-null capture callback before launch. Use the builder
handler when the capture callback is omitted/null. Do not silently override either registration or invoke two handlers
for the same rejected exit. The agreed validation/handler policy below records all four cases.

## Output order: observation versus original writes

Each `CapturedStream.Text` alone retains that stream's text without the interleaving. The current
[supervisor](../../src/Sotsera.Rafter/Internal/ProcessOperation.cs) reads the two streams
independently. Inferring a total order of the child's original stdout/stderr writes from those observations is not
reliable: each stream is buffered and readers can be scheduled independently. Byte-mode pipe reads also do not retain
the child's individual write boundaries. Microsoft's [redirected-stream documentation](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandarderror?view=net-10.0)
describes independent/concurrent draining, and the [pipe-mode documentation](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-type-read-and-wait-modes)
explains the absence of byte-pipe write boundaries; the ordering limitation is a consequence of these separate paths.

For example, a child may write stdout A, stderr B, stdout C, but the stdout reader may receive A and C together before
the stderr reader records B. A parent-side timestamp or sequence number cannot recover the missing original interleave.

The agreed per-stream `Chunks` collections retain shared `Order` numbers and exact decoded text. These numbers
express the order Rafter records chunks across both readers, with each stream's own order preserved. Assign the order
and record its chunk consistently; merely numbering independently scheduled echo operations would not establish a
matching transcript. Chunk boundaries are reader observations, not promises about lines, original write calls, or
individual log events. An illustrative combined transcript:

```text
1  stdout  Building...\n
2  stderr  Warning...\n
3  stdout  Done.\n
```

Keep `Text` convenient for structured parsing and `Chunks` useful for inspecting mixed diagnostics.
If echo is enabled, use this same recorded sequence for child echo, so replay reflects the observed
transcript. That coordination must retain independent lifecycle supervision and await all output consumers.

Both views must share the bounded capture storage/limits, with bounded chunk metadata and no independent unlimited
transcript. Source-level merging could give one combined stream but would
discard stdout/stderr identity; it is a separate mode, not a way to reconstruct the original interleave afterward.

## Agreed capture limits and overflow

Agreed on 2026-10-23; this is a design contract, not an implementation claim:

- Require an explicit positive limit, applied independently to stdout and stderr. Unused capacity in one stream does
  not increase the other's allowance. Count UTF-16 code units, as in `Text.Length`. The example's 1024 remains a script
  default; the library supplies no capture-limit default.
- Exactly the configured limit is valid; additional decoded output exceeding it triggers overflow. Enforce the limit
  and bound chunk metadata while reading. `Text` and `Chunks` must agree on the retained content.
- On overflow, stop retaining new data and echoing new child chunks for both streams. Initiate shutdown of only that
  owned process scope, keep draining/discarding both pipes, and finish already queued echo work before releasing owners.
- After clean settlement, throw `ProcessCaptureLimitException`. Return no truncated normal `CapturedProcessResult`
  and do not invoke the rejected-exit callback, including when the root exits with an accepted code.
- Keep bounded partial stdout/stderr snapshots available through the exception, clearly identified as incomplete.
  Do not automatically print their contents in framework diagnostics. Their property names and metadata are agreed
  below.
- Catching a cleanly settled limit exception can recover, consistently with process timeout recovery. An unhandled
  limit failure returns invocation exit `1`. Catching cannot erase prior sticky failure or invocation cancellation.

These are logical retained-text bounds, not exact byte-allocation quotas. Both views and their metadata must be
bounded, including any concatenated string allocation. As an implementation recommendation, retain no empty chunks:
chunk count then cannot exceed retained code-unit count. Shared retained data and a cached/materialized text view can
avoid repeated independent copies. No separate public chunk limit is part of this slice.

Termination uncertainty and independent output/echo failures retain their existing settlement-failure obligations.
The agreed overflow/timeout/cancellation arbitration is recorded below; a full capture buffer does not establish
process/output completion.

Later verification should separately cover exact-limit success, one-code-unit overflow on either stream, unused
capacity in the other stream, partial chunks, Unicode boundaries, metadata bounds, continued draining, recovery,
and combined stop/failure cases.

## Agreed Unicode boundary policy

Keep the limit measured in UTF-16 code units, while ensuring that retention and chunk creation never split
a valid surrogate pair. A scalar that needs two units must fit as a whole; otherwise keep the shorter prefix and commit
overflow. Do not exceed the limit to finish it, replace a half pair with a new character, or skip the scalar and capture
later text. The exception's partial text can therefore be shorter than its configured limit:

| Decoded output | Limit | Retained prefix | Outcome |
| --- | ---: | --- | --- |
| `A😀` | 3 | `A😀` | Fits exactly. |
| `A😀` | 2 | `A` | Overflow; the two-unit emoji does not fit. |
| `A😀B` | 3 | `A😀` | Overflow on the following `B`. |

Apply the same scalar-boundary policy to individual `CapturedChunk.Text` values, not only the concatenated `Text`.
As an implementation approach, if a reader boundary falls between a high and low surrogate, hold at most the trailing
high surrogate for that stream until the next read can complete it. Assign `Order` when a recordable chunk is accepted;
this bounded buffering can change chunk grouping/interleaving and still satisfies the agreed observation-order
contract. If capture freezes before the pair completes, omit the pending half from the published partial snapshot.

Preserving surrogate pairs does not promise to preserve an entire grapheme cluster, combining sequence, or multi-scalar
emoji. The existing decoder's behavior for malformed encoded input is separate; limit handling should not invent
replacement text. See Microsoft's [character-encoding guide](https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding-introduction)
for UTF-16 units, surrogate pairs, and grapheme clusters. The boundary guarantees are agreed; buffering mechanics
remain an implementation choice to verify.

## Agreed capture-limit exception details

Use a public sealed
`ProcessCaptureLimitException : Exception`, with internal construction and immutable properties, following the existing
process-exception pattern:

| Property | Type | Meaning |
| --- | --- | --- |
| `Executable` | `string` | Executable identity, without automatically exposing argument/environment contents. |
| `Stream` | `ProcessOutputStream` | The first stream whose overflow Rafter committed: `StandardOutput` or `StandardError`. |
| `Limit` | `int` | Configured UTF-16 code-unit limit for each stream. |
| `PartialStandardOutput` | `CapturedStream` | Non-null frozen retained stdout prefix and its chunks; possibly empty. |
| `PartialStandardError` | `CapturedStream` | Non-null frozen retained stderr prefix and its chunks; possibly empty. |
| `ObservedExitCode` | `int?` | Root exit code when known, even if zero; not proof of successful capture. |

Both partial streams retain the shared `Order` sequence and `Text`/`Chunks` consistency, and freeze at overflow
commitment. They are not complete captures, and no normal process `Result` is attached. Do not add a claimed final
output length: Rafter stops retaining and discards subsequent output, so it cannot infer that length from the prefix.
The overflowing stream's retained length may be below `Limit` under the agreed Unicode policy.

Recommend a concise exception message naming the executable, overflowing stream, and configured limit. Framework
output should identify a capture-limit failure distinctly, omit an unexpected-exception stack trace for this recognized
failure, and avoid printing retained text automatically. Detailed diagnostics and combined failure/race precedence
are agreed below. The properties, enum type name, and their meanings are agreed.
These members are implemented; the guide links their verification coverage.

## Agreed overflow, timeout, and cancellation arbitration

Extend the [agreed process-timeout race rules](process-timeouts.md#agreed-race-handling) with one serialized stopping
decision per operation. Agreed on 2026-10-23; capture implementation and verification remain pending:

- Prefer invocation cancellation already requested at the decision point when no timeout/overflow cause has been
  committed. Do not manufacture a capture-limit failure during cancellation shutdown merely because output continues.
- Otherwise, preserve the first timeout or overflow cause committed by Rafter. A later event cannot replace it, and
  comparing wall-clock estimates after the fact must not manufacture a different ordering. If timeout and overflow
  are both pending without cancellation, whichever commits first wins; an elapsed deadline alone is not commitment.
- A timeout can commit only while owned execution remains active, following the existing completion recheck. Overflow
  concerns retained output, so it can still occur while draining after the owned scope is confirmed empty. In that case
  the execution timer is already disarmed and cannot turn the capture failure into a timeout.

| Observed/committed ordering | Primary process-call outcome after clean settlement |
| --- | --- |
| Cancellation requested before overflow commits | Existing cancellation outcome; normally invocation exit `130` when no independent failure takes precedence. |
| Overflow commits, then the execution deadline passes | `ProcessCaptureLimitException`; do not add a new timeout or deadline notice during overflow shutdown. |
| Timeout commits, then capture exceeds its bound | Preserve `ProcessTimeoutException`; enforce capture bounds without adding a second limit exception. |
| Overflow commits, then invocation cancellation is requested | Preserve the limit exception. Uncaught: invocation exit `1`. Caught with otherwise clean cancellation: exit `130`. |
| Owned execution is complete, then remaining output exceeds the bound | `ProcessCaptureLimitException`; root exit zero and elapsed wall time do not turn this into success or timeout. |

Limit enforcement remains unconditional, regardless of which cause wins. When the bound is exceeded, freeze both
retained streams and new child echo, and drain/discard subsequent data. If cancellation or timeout is primary, retain
the overflow as secondary context rather than returning success, growing buffers, or replacing that primary exception.
The agreed capture-specific exceptions below expose partial data and secondary overflow context while preserving
the primary outcome.

Normal root exit, overflow, timeout, and cancellation reuse shutdown already in progress and the earliest applicable
grace and verification/drain deadlines. No later cause restarts or extends either budget. A second Ctrl+C still skips
remaining grace without changing a committed timeout/overflow cause or restarting verification. Per-process overflow
does not cancel unrelated operations; invocation cancellation continues to close launch admission and later scheduling.

After settlement, a caught limit exception can recover only when no independent failure or invocation cancellation
remains. Catching cannot restore success after cancellation. Timeout-first followed by cancellation keeps the existing
timeout contract: uncaught timeout means exit `1`, while caught timeout plus otherwise clean cancellation means `130`.

**Termination unknown**, **Output incomplete**, echo failures, and independent sticky failures keep their existing
precedence and ownership obligations. Intentional bounded discarding after overflow is part of controlled shutdown;
it must not itself be confused with a failed pipe read or an unjoined output operation. If settlement cannot be confirmed,
report the appropriate settlement failure with timeout/overflow context, not a clean recoverable limit exception.
Avoid duplicate final process errors. Captured timeout/cancellation secondary-overflow properties and diagnostic
wording are agreed below; public capture-data access on settlement failures is deferred.

Verification should control the event order for each case above, including cancellation before commitment, both
timeout/overflow commitment orders, overflow during post-exit draining, and cancellation after a caught limit failure.
It should also verify bounded retention when a different cause wins, unchanged shutdown budgets, no rejected-exit
callback for these outcomes, and settlement failures overriding the clean-stop paths. This policy is implemented for
capture; the guide links controlled and native verification coverage.

## Agreed validation timing and callback interaction

Agreed on 2026-10-23. Keep capture execution configuration local to that call, using this signature shape (the
containing result relationship is agreed):

```csharp
Task<CapturedProcessResult> RunAndCaptureAsync(
    int maxCharactersPerStream,
    bool echo = false,
    Func<CapturedProcessResult, ProcessFailureAction>? onFailure = null);
```

### Validation before process work

- Validate the positive limit at the `RunAndCaptureAsync(...)` call boundary. Use
  `ArgumentOutOfRangeException` for zero or negative values, synchronously before returning an operation task,
  matching existing immediate process-setting checks.
- Capture one consistent builder snapshot, including any configured `OnFailure`. Reject a non-null capture callback
  combined with that handler using `InvalidOperationException`, before registering a process operation, producing a
  process-start heading, creating buffers/pipes, or launching a child. Check the conflict regardless of eventual exit code.
- Treat omitted or explicitly null `onFailure` as no capture-specific handler. Existing builder `OnFailure(null)`
  retains its current immediate null rejection; the execution argument is optional, not a new builder registration.
- A failed validation must leave the builder, its handler, and process admission unchanged. The body may catch the
  error and make a valid call. Failed validation creates no tracked child work to settle.
- Capture successful per-call arguments and the builder settings before asynchronous launch. Later builder edits do
  not affect the captured operation. The per-call capture callback must not become the builder's persistent handler.
- Repeated valid `RunAndCaptureAsync` calls execute separate operations with their own capture limits, buffers, order
  counters, and callback snapshots. They are executions, not repeated configuration calls to reject.
- Keep input and body validation distinct: an option `.Validate(...)` may reject user input before entering the body
  with exit `2`; invalid process-call arguments arise when the body makes that call, and an unhandled configuration
  exception is contained by outer command `RunAsync` as exit `1`, before the invalid operation launches.

### Exactly one rejected-exit handler

| Builder `OnFailure` | Capture-call `onFailure` | Agreed behavior |
| --- | --- | --- |
| Not configured | Omitted/null | Existing default `Throw` for rejected exits. |
| Configured | Omitted/null | Use the builder handler once, with its existing `ProcessResult` parameter. |
| Not configured | Non-null | Use the typed capture callback once, with `CapturedProcessResult`. |
| Configured | Non-null | Reject before launch; do not choose one or invoke both. |

These rules allow helpers to configure a shared metadata-only rejection policy, while capture-aware scripts can
inspect `StandardOutput.Text`, `StandardError.Text`, and their chunks directly through the typed callback. If the
result subtype is used, the builder handler also receives that same captured-result object through its
base-type parameter.

Choose the effective handler at validation/snapshot time, then invoke it only after complete capture and owned
process/output settlement, and only for a rejected exit. Accepted exits, configuration/launch failures, overflow,
timeout, cancellation, and output/echo failures do not invoke it. No handler automatically replaces a previous one,
including when delegates happen to refer to the same method.

Keep the callback synchronous and reuse existing `Continue`, `Fail`, and `Throw` behavior. A thrown
callback exception or an undefined `ProcessFailureAction` follows the existing handler-failure path, preserving the
original rejection and callback error; it must not silently become `Continue` or run a second handler. Invocation
cancellation and independent sticky failures remain effective during/after callback execution.

Future analyzers may diagnose provably invalid constants or handler conflicts; runtime checks remain required for
dynamic values and shared builders. Implementation should also extend existing discarded-process-task diagnostics
to recognize the new execution method, and retain body ownership/tracking of its full asynchronous operation.
Runtime checks and the RAF003 extension implement this section; the guide links their tests.

## Agreed partial capture on timeout and cancellation

The following decisions address data access, without changing the stopping-cause and invocation-outcome rules.
Keep exception payloads separate from normal successful/rejected-exit results.

### Capture-specific exception shapes

Use these public sealed exception types for capture execution:

| Exception | Base type | Partial-data access |
| --- | --- | --- |
| `CapturedProcessTimeoutException` | `ProcessTimeoutException` | Non-null `PartialStandardOutput` / `PartialStandardError`, both `CapturedStream`. |
| `CapturedProcessCanceledException` | `OperationCanceledException` | The same two non-null `CapturedStream` properties. |

The timeout subtype inherits `Executable`, `Timeout`, `Elapsed`, and `ObservedExitCode`. It requires removing
`sealed` from the existing `ProcessTimeoutException`, keeping construction internal. Existing catches for that type
or `TimeoutException` continue to match. This public-type change and the new subtype names are implemented.

The cancellation subtype exposes `Executable` and nullable `ObservedExitCode`, and carries the exact invocation
cancellation token through the `OperationCanceledException` base. That preserves existing cancellation recognition,
canceled-task semantics, and command exit `130` when no independent failure takes precedence. Keep construction internal.
For cancellation before native launch, use empty partial streams if the capture call has passed validation;
do not allocate native resources just to produce a diagnostic payload. Validation errors remain configuration errors.

These capture-specific types keep partial streams statically non-null without adding nullable capture properties to
every ordinary process timeout. Empty capture is represented by empty `Text` / `Chunks`. Shared timeout/cancellation
catch categories remain available. These are agreed design contracts, not verified runtime behavior.

### What partial means and when it freezes

- A timeout/cancellation snapshot contains the bounded decoded output actually retained before settlement. It may
  include shutdown diagnostics. It does not claim the interrupted tool completed its intended output or flushed all
  application buffers, even when Rafter confirmed EOF on both pipes.
- Continue capture and optional echo during graceful shutdown and draining, within the agreed bounds.
  Freeze the immutable snapshots after readers/output consumers settle. If overflow is observed earlier, use the
  agreed freeze-and-discard behavior from that point instead. Unpaired pending surrogate halves are never published.
- Preserve `Text` / `Chunks` consistency and the shared `Order` sequence. Do not return a normal `CapturedProcessResult`
  or label these snapshots as complete results. Neither stopping path invokes the rejected-exit callback.
- If timeout is primary, throw the captured timeout subtype after clean settlement. If invocation cancellation is
  primary, throw the captured cancellation subtype. A later overflow or cancellation cannot replace a committed
  timeout/overflow cause, under the agreed race policy.
- Catching the timeout can recover when no independent failure/cancellation remains. Catching the cancellation
  permits inspecting diagnostics but cannot clear invocation cancellation, reopen launch admission, or restore exit `0`.
- Settlement uncertainty, failed output/echo, and independent sticky failures retain precedence. Do not present them
  as a clean settled capture timeout/cancellation just to attach text. Public capture-data access on settlement-failure
  exceptions is deferred; retain bounded immutable internal diagnostic context and never expose mutable producer buffers.

### Secondary overflow context

Add `CaptureLimit` (the configured per-stream limit) and nullable `OverflowedStream` to each captured
timeout/cancellation exception. A non-null stream identity means capture also exceeded its bound during shutdown;
the retained prefix froze then. Null means no capture overflow was observed; it still does not make interrupted
output a complete result. These member names and meanings and the `ProcessOutputStream` enum name are agreed.

This context explains discarded data without changing the primary exception or creating a second final error.
As with the limit exception, do not claim a final total output length or automatically print retained text. Concrete
tests should cover final shutdown output, empty/prelaunch cancellation, Unicode-safe partials, both primary causes,
secondary overflow, matching cancellation tokens, recovery/exit outcomes, and immutable snapshots after settlement.
This capture behavior is implemented; verification scope is recorded in the guide.

## Agreed capture-failure presentation

Agreed on 2026-10-23. These transcripts illustrate the implemented display contract; they are not captured executions.
Use the existing flat two-space process gutter, stderr routing, wrapping, literal-text handling, and final invocation
summary. Keep raw captured text out of automatic framework diagnostics in every mode, including verbose. Authors can
explicitly inspect/print the streams or choose live echo; framework output does not automatically replay them.

### Overflow progress and final failure

When overflow becomes the primary committed cause, emit one yellow progress notice in normal/verbose stderr:

```text
  Capture limit exceeded | dotnet
```

Emit it only on actual overflow, not when retained output merely reaches the allowed limit. It announces the stopping
event, not confirmed settlement. Suppress it in quiet mode. If overflow is secondary to timeout/cancellation, preserve
that primary event and report secondary context in its applicable final metadata rather than adding another notice.

If the settled limit exception escapes the body, emit a red heading followed by ordinary metadata:

```text
  Process capture limit exceeded | dotnet
  Stream: stdout | limit: 1024 UTF-16 code units
  Partial capture (UTF-16 code units): stdout 1024 | stderr 85
  Observed exit code: 1
```

The counts are retained `Text.Length` values, not the child's total generated output, message count, or allocation size.
Unicode-safe clipping can leave the overflowing stream below its limit. Include both streams even when a count is zero;
omit the observed-exit line when unknown. Retain this final error and the failed summary in quiet mode. Recognized limit
exceptions receive no unexpected-exception stack trace. A caught settled overflow has no automatic final red block or
handled-overflow warning; the yellow progress notice can remain because it describes an actual event.

### Timeout and secondary overflow

Retain the agreed deadline-progress notice and timeout heading. For an unhandled captured timeout, append partial
capture counts and, when applicable, secondary overflow metadata within the same error block:

```text
  Process timed out | dotnet
  Timeout: 10s | elapsed: 10s250ms
  Partial capture (UTF-16 code units): stdout 240 | stderr 1024
  Capture limit also exceeded: stderr | limit: 1024 UTF-16 code units
  Observed exit code: 1
```

Omit the secondary-overflow line when none was observed. Keep one primary heading, no second capture-limit failure
block, and no automatic text replay. Catching a clean settled timeout suppresses this final error block under the
existing recovery contract; its exception still exposes the partial streams and secondary metadata.

### Cancellation

In normal output, retain the existing yellow invocation `Cancellation requested.` notice and final cancelled summary.
Add no extra per-process cancellation block by default. In verbose mode, add a factual record after captured
process/output settlement, with a yellow heading and ordinary metadata:

```text
  Process cancelled | dotnet
  Partial capture (UTF-16 code units): stdout 240 | stderr 36
```

Include secondary-overflow and known-exit metadata here when applicable. This is a settled lifecycle record, not an
unexpected-exception error; it can remain if script code catches the cancellation, since invocation cancellation is
still effective. Quiet suppresses this verbose detail and the progress notice, while retaining the cancelled summary.
Independent failures retain their existing failure/exit precedence and must not be labeled clean cancellation.

### Other outcomes and modes

- Accepted captured exits keep ordinary lifecycle/verbosity behavior and script-authored results. Include complete
  capture counts in verbose mode only; capture alone does not introduce extra normal-mode completion messages.
- Rejected exits keep the existing `Process failed` / `Process failure handled`, exit-code, and action diagnostics.
  Append complete capture counts to those rejection blocks when emitted, without dumping text. Unlike
  interrupted captures, those retained streams are complete; do not label them partial. Caught `Throw` still has no
  final rejection block, while `Continue` / `Fail` keep their existing diagnostics, including in quiet mode.
- **Termination unknown** and **Output incomplete** retain their original heading and failure status. Add available
  bounded capture/stop context under that heading without presenting a second clean timeout/overflow failure.
- Unexpected callback exceptions retain existing callback-failure details and stack traces. Avoid interpreting that
  policy as automatic permission to dump captured streams; script-authored exception contents remain script output.
- `--plain` and redirected destinations preserve the selected words/data without styling or presentation prefixes.
  `NO_COLOR` disables color under the existing policy. Quiet/verbose select information; plain does not select different
  capture data. Echo remains an explicit independent capture setting and follows the recorded child-chunk sequence.

Verify this contract during implementation, including normal/quiet/verbose, redirected/mixed destinations,
Unicode-short prefixes, empty captures, caught recovery, secondary overflow, and settlement-failure precedence.

## Example scenario

The example launches its own compiled assembly through `dotnet`, using separate argument tokens. A small child branch
emits JSON; the Rafter body captures it, parses it, and prints the greeting. Selecting `Failure` emits stderr and exits
`7`; the existing default rejected-exit behavior unwinds before JSON parsing. Selecting `Overflow` emits 2048 code units,
exceeding the example's default limit. The successful child also writes a diagnostic to stderr. `--echo` demonstrates
optional live echo, `--limit` changes the per-stream bound, and `--show-order` displays the two chunk collections merged
by `Order`, labeling their source stream and escaping text so partial lines and line endings remain visible.

Using the assembly path avoids assuming that `Environment.ProcessPath` identifies a standalone script executable.
Real file-app and project consumers verify this launch shape. Help retains input descriptions without launching the child.

The example demonstrates the normally returned successful result and the existing default rejected-exit flow. The
shared rejected-exit exception access above is implemented and tested. The example deliberately leaves overflow
unhandled to demonstrate framework diagnostics. Timeout/cancellation partials are exercised in the native/API checks.

## Final decisions before the implementation issue

Agreed on 2026-10-23. The recommendations in this section are accepted. Output presentation, handler selection,
validation, Unicode retention, stopping-cause arbitration, and partial timeout/cancellation contracts are agreed;
they do not need another design round.

| Decision | Agreed choice | Consequence |
| --- | --- | --- |
| Result relationship and rejected-exit access | Seal `CapturedProcessResult` and derive it from `ProcessResult`; remove `sealed` from the base while keeping construction internal. | The typed callback and normal return expose streams directly. Existing `ProcessFailureException.Result` and builder handlers preserve the same captured object through their base-type signatures; catch code uses a type pattern. No additional rejected-exit exception is needed. |
| Remaining public data types | Use immutable `CapturedChunk` with `long Order` and `string Text`, exposed as `IReadOnlyList<CapturedChunk>`. Use `ProcessOutputStream` with `StandardOutput` / `StandardError` for exception stream identities. | Finish the public surface without adding a combined transcript property. Read-only collections must also prevent mutation through their underlying storage. |
| Separate chunk limit | Keep the explicit character limit as the only public capture budget in this slice. Retain no empty chunks, so chunk count cannot exceed retained UTF-16 code units. | Bound metadata and materialized text internally, including worst-case one-code-unit chunks; verify that bound. This is not an exact allocation-byte quota. |
| Partial data when settlement fails | Keep the existing sticky settlement-failure path and defer a new public exception API for its captured streams. Retain bounded, immutable diagnostic context internally, including counts and the committed stopping cause. | `Termination unknown` / `Output incomplete` retain priority and available metadata. Do not substitute a recoverable capture-limit/timeout exception or expose buffers still being written. Script access to partial text on these paths is outside the first slice. |

The result choice makes the alternative `GetCapturedOutput()` design unnecessary. The already
agreed `CapturedProcessTimeoutException : ProcessTimeoutException` change is separate and does not need reconfirmation.
The settlement-failure choice is a scope boundary, not a claim that all failure paths expose captured text.

[Issue #44](https://github.com/sotsera/rafter/issues/44) implements these decisions. Verification covers exact text and empty streams,
separate stdout/stderr, optional echo, per-stream bounds and overflow, concurrent isolation, rejected exits, timeout,
cancellation, termination/output uncertainty, and actual file-app/project consumers. Keep portable buffer/type checks
separate from actual Windows owned-process evidence. The guide links executable checks; exact-commit results belong to the PR.
