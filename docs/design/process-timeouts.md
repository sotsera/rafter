# Per-process timeouts

Agreed on 2026-10-22 and implemented for [issue #42](https://github.com/sotsera/rafter/issues/42).
The [runnable example](../../examples/process-timeout.cs) and [implementation guide](../guides/process-timeouts.md)
show the delivered API. The preserved [reference](../../reference/examples/process-timeout.cs) supplies a scenario,
not an API requirement. The rules below remain the agreed contract; verification is recorded separately.

## Agreed scope and syntax

- Begin with a timeout on one process operation, configured through `.Timeout(TimeSpan)` on its process builder.
  An option value is read explicitly: `.Timeout(context.GetValue(timeout))`.
- There is no timeout by default. Configured timeout durations must be positive; zero and negative values are invalid.
  Omitting `.Timeout(...)` leaves the process without this execution deadline.
- Start the deadline when the child begins execution, not when its command body begins or the builder is configured.
- Expiry initiates shutdown of that process and its owned descendants using the existing scoped shutdown, termination
  verification, output handling, and settlement rules. Do not stop unrelated process operations merely because this
  process reached its deadline.
- The deadline starts shutdown; it does not promise that process `RunAsync` returns at that exact instant. Owned work
  must settle before completion is reported. Termination uncertainty and incomplete output retain their existing status.
- Report timeout distinctly from user cancellation. An unhandled timeout fails the invocation with exit `1`.
  Ctrl+C retains the existing invocation-cancellation contract, normally exit `130` when no failure takes precedence.
- `OnFailure` remains the handler for rejected exit codes. It does not handle timeouts or turn them into accepted exits.
- Target, whole-invocation, and cleanup deadlines remain separate topics. This first slice adds no generic managed
  callback timeout, cleanup API, or new native process platform.

The example builds a user-selected project with a declared timeout option:

```csharp
await context.Executable("dotnet")
    .Arguments("build", context.GetValue(project))
    .Timeout(context.GetValue(timeout))
    .RunAsync();
```

The example's `--timeout` option defaults to ten seconds and validates positive values before entering the body.
That script-level choice does not introduce a library-wide timeout default. The example deliberately lets an unhandled
timeout unwind the body; the success message appears only after a successful process call. It requires the existing
Windows process runtime and a buildable project, like the current process-failure example. The build's duration is
not deterministic; controlled child fixtures should verify deadline behavior during implementation.

## Agreed recovery behavior

Keep recovery consistent with the existing catchable process-exception boundary:

- Throw a specific `ProcessTimeoutException` inside the body after the owned process scope and output have settled.
  Do not normally return a `ProcessResult` for a timed-out operation or treat an observed exit code as proof of success.
  Its inheritance, metadata, and reporting are defined below.
- Catching a successfully settled timeout can recover. The body may choose a valid fallback or launch a fresh process
  with its own deadline. The timeout does not cancel the whole invocation or permanently close unrelated launch admission.
- A handled timeout alone does not record irreversible invocation failure. The command can return `0` and later targets
  can run if the body succeeds and no independent failure or invocation cancellation remains.
- Defer the framework timeout-failure diagnostic until the exception escapes the body, following existing `Throw`
  reporting. Preserve original child output; catching the timeout should not produce an automatic framework error or
  unexpected-exception stack trace. The agreed yellow shutdown-progress notice is a separate operational event.
- An uncaught timeout unwinds the body, runs ordinary C# `finally` blocks, prevents later targets, and is contained by
  command `RunAsync` as failure with exit `1`.
- Catching cannot erase a recorded `ProcessFailureAction.Fail`, invocation cancellation, **Termination unknown**, or
  **Output incomplete**. Incomplete settlement must not be presented as a clean, recoverable timeout or authorize
  abandoning readers/cleanup. Its existing failure/settlement obligations remain in force.

This recovery contract is implemented. It does not add a timeout callback to `OnFailure`.

## Agreed exception details

Use a public sealed `ProcessTimeoutException` derived from `System.TimeoutException`, so scripts can catch either the
Rafter-specific failure or the general timeout category. Keep construction internal, following the existing process
exception pattern. Read-only properties:

| Property | Type | Meaning |
| --- | --- | --- |
| `Executable` | `string` | The executable identity used for this process call. |
| `Timeout` | `TimeSpan` | The configured execution timeout. |
| `Elapsed` | `TimeSpan` | Time from child execution start through confirmed owned process/output settlement, including shutdown time. |
| `ObservedExitCode` | `int?` | The observed root exit code when available; null when unavailable. It is not a successful-process result. |

Do not add a `ProcessResult` property for the timed-out operation. An observed exit code of zero remains compatible
with timeout: a child may acknowledge shutdown by returning zero after its deadline. The timeout stays the cause of
the exception and does not invoke the rejected-exit handler. Arguments and environment values should not be included
automatically in the exception message or framework output.

These details do not change the requirement that a clean timeout exception follows confirmed
settlement; termination uncertainty and incomplete output retain their independent failure handling.

## Agreed output

At expiry, emit one yellow operational notice on stderr in normal/verbose modes while beginning owned-scope shutdown:

```text
  Process deadline reached | dotnet
```

This announces a deadline event, not confirmed termination or a final invocation failure. It can appear even if script
code later catches the timeout. Quiet mode suppresses this notice, following the existing cancellation-progress policy.

If the settled timeout exception escapes the body, emit a concise red process heading followed by ordinary metadata:

```text
  Process timed out | dotnet
  Timeout: 10s | elapsed: 10s250ms
  Observed exit code: 0
```

Use exact compact duration formatting for these details. Show the observed-exit line only when a code is known.
Keep the flat two-space process gutter, existing wrapping/literal-text behavior, and stderr routing. Retain the final
error in quiet mode and the existing failed-invocation summary; omit an unexpected-exception stack trace for this
recognized failure. Plain/redirected output keeps the same words without color or emphasis.

When script code handles the exception, do not emit the final red timeout block, a framework stack trace, or an
automatic handled-timeout warning. The script can describe its retry/fallback, and a recovered invocation can finish
successfully under the agreed outcome rules. Preserve all original child output and independent recorded failures.
Termination unknown and output incomplete remain distinct diagnostics; do not substitute this clean-timeout block for them.

These transcripts illustrate the agreed output contract; they are not captured executions.

## Agreed race handling

Treat process execution, output settlement, and invocation cancellation as related but separate state. Make one
serialized decision about the process's stopping cause; subsequent shutdown events must not rewrite an already
committed timeout or create a second timeout for the same operation. These rules describe observable arbitration,
not a promise to reconstruct the exact OS exit time from delayed notifications or polling.

### Execution completion and the deadline

- Keep the execution deadline active until the root and all owned descendants have stopped, as confirmed by the
  owned scope. Root exit alone starts existing descendant cleanup but does not establish execution completion.
- Before committing a timeout at/after its deadline, recheck whether the owned scope is already empty. If completion
  is confirmed and no timeout has been committed, do not manufacture a timeout from a delayed timer notification.
- Disarm the execution timeout once owned execution is confirmed complete. Output may still need to drain, using
  the existing verification/drain policy. Slow output alone must not be reclassified as a process execution timeout.
- If timeout is committed while owned execution remains active, subsequent exit, including exit zero during shutdown,
  does not erase it. After clean settlement, the call throws `ProcessTimeoutException` under the agreed contract.

### Invocation cancellation and timeout

- If invocation cancellation is already requested before the timeout is committed, cancellation wins for that process.
  Do not add a timeout merely because its execution deadline passes during cancellation shutdown.
- If cancellation and deadline expiry are both pending at the decision point with no timeout already committed,
  prefer cancellation. This avoids turning an expected user interruption into a new timeout failure.
- If timeout is committed first and Ctrl+C follows, preserve the process timeout. Invocation cancellation still stops
  new launches and later targets. An unhandled timeout produces failure/exit `1`; a caught timeout followed by otherwise
  clean invocation cancellation produces cancelled/exit `130`. Catching cannot restore success after Ctrl+C.
- Invocation cancellation remains effective during output draining or after this process has completed; absence of a
  timeout does not imply that the whole invocation can succeed after cancellation.

### Settlement and shutdown budgets

- Reuse shutdown already in progress. Timeout, normal root exit, and subsequent cancellation must not restart or
  extend grace or verification/drain deadlines; use the earliest applicable deadlines for the affected operation.
  A second Ctrl+C still skips remaining grace without restarting verification.
- **Termination unknown**, **Output incomplete**, and independent recorded/unhandled failures keep their existing
  precedence. A cleanly settled timeout exception is not a substitute for a settlement failure. Preserve timeout
  context when reporting such combined conditions without duplicating final process errors.

This race policy is implemented. Controlled tests should exercise event orderings and delayed
observations without relying only on close wall-clock sleeps.

## Agreed repeated-call and validation rules

- Permit one successful `.Timeout(TimeSpan)` configuration per `ProcessCommand` instance. Reject a second call even
  when it supplies the same duration, including calls through aliases or helper methods using that same instance.
  A fresh builder may choose a different timeout.
- Match existing process-setting validation: reject nonpositive values immediately in `.Timeout(...)` with
  `ArgumentOutOfRangeException`, and repeated configuration with `InvalidOperationException`. These are configuration
  errors, not `ProcessTimeoutException` or rejected-exit results; do not invoke `OnFailure`.
- Accept any positive `TimeSpan`, including `TimeSpan.MaxValue`. An underlying timer's scheduling limit must not become
  an arbitrary public duration limit. Omitting the setting remains the way to request no execution timeout.
- Failed configuration leaves the builder's prior valid state unchanged. An invalid first attempt does not store a
  timeout; catching it can allow a later valid configuration. A repeated-call error does not replace the first value.
- Process `RunAsync` captures an immutable configuration snapshot and validates it before launching that operation.
  Already-running operations retain their captured deadline; later builder changes cannot alter them.
- Keep the two validation boundaries distinct. A command option's `.Validate(...)` rejects invalid user input during
  command preparation (exit `2`, before the body). A process-builder error arises inside the executing body; if caught,
  recovery is possible, and if unhandled the existing command boundary contains it as failure/exit `1` before that
  invalid configuration launches a child. It cannot be validated before the outer command body creates the builder.

These configuration rules are agreed. Future analyzers may diagnose repeated calls or constant
nonpositive values they can prove; runtime checks remain necessary for dynamic values and shared builder references.

## Implementation scope

[Issue #42](https://github.com/sotsera/rafter/issues/42) tracks this contract. The
[implementation guide](../guides/process-timeouts.md#verification) links actual Windows owned-process/descendant tests,
file-app/project consumers, recovery/failure, cancellation races, output settlement, and portable configuration checks.
