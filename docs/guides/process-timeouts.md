# Process execution timeouts

Implemented for [issue #42](https://github.com/sotsera/rafter/issues/42), following the
[agreed design](../design/process-timeouts.md). The [runnable example](../../examples/process-timeout.cs) builds a
user-selected project using a positive duration option. Owned process execution currently requires Windows.

```csharp
await context.Executable("dotnet")
    .Arguments("build", context.GetValue(project))
    .Timeout(context.GetValue(timeout))
    .RunAsync();
```

There is no deadline unless configured. One successful `Timeout(TimeSpan)` call is permitted per builder, including
calls through aliases/helpers. Nonpositive values throw `ArgumentOutOfRangeException` immediately; repeated calls
throw `InvalidOperationException`. Failed calls leave prior configuration unchanged. Any positive `TimeSpan` is valid,
including `TimeSpan.MaxValue`. Each process run captures its configuration; later builder edits cannot change that run.

Option validators can reject invalid user input before the body (exit `2`). Builder errors occur when the body
configures its process; they are catchable and an unhandled configuration error produces exit `1` before launch.

## Execution and recovery

The deadline begins when the child is released to execute, after native launch preparation. It covers the root and
owned descendants until their scope is confirmed empty. Expiry initiates the existing graceful/forceful shutdown and
output settlement. It does not promise a return at the configured instant. Monitoring uses monotonic elapsed time
and the existing supervision loop; scheduling delays are possible. Slow output after confirmed execution completion
uses the existing drain policy and is not an execution timeout.

After clean settlement, the process call throws public `ProcessTimeoutException : TimeoutException`, exposing
`Executable`, `Timeout`, `Elapsed`, and nullable `ObservedExitCode`. Elapsed includes shutdown and output settlement.
An observed exit zero does not erase a committed timeout. No process result is returned and `OnFailure` is not invoked.
The base type has internal construction and supports the sealed
[`CapturedProcessTimeoutException`](process-capture.md) subtype for captured execution.

```csharp
try
{
    await context.Executable("tool").Timeout(TimeSpan.FromSeconds(10)).RunAsync();
}
catch (ProcessTimeoutException exception)
{
    context.WriteLine($"{exception.Executable} exceeded its execution deadline; using a fallback.");
    await context.Executable("fallback-tool").RunAsync();
}
```

A caught timeout alone permits success, new process launches, and later targets. An unhandled timeout unwinds normal
C# `finally` blocks, prevents later targets, and is contained by command `RunAsync` as exit `1`. Catching cannot erase
earlier sticky failures, invocation cancellation, termination uncertainty, or output incompleteness.

## Cancellation and settlement

The supervisor rechecks owned completion before committing expiry. Completion already observed at that decision
disarms the timer. Invocation cancellation and timeout commitment share an arbitration gate: cancellation requested
first wins, while a committed timeout remains the process outcome if cancellation arrives later. A caught timeout
followed by otherwise clean cancellation returns `130`; an uncaught timeout returns `1`.

Normal root exit, timeout, cancellation, and repeated interrupts reuse the earliest existing shutdown budgets.
Root exit alone does not complete a scope containing descendants. **Termination unknown** or **Output incomplete**
remain sticky settlement failures and retain timeout metadata when combined; they are not recoverable clean timeouts.
All owned reader and reporting operations are joined before resources are released, even if a writer blocks.

## Output

Normal/verbose stderr emits the yellow operational notice `Process deadline reached | tool` once at commitment.
Quiet suppresses this notice. A caught timeout has no automatic final error block. An unhandled timeout produces:

```text
  Process timed out | tool
  Timeout: 10s | elapsed: 10s250ms
  Observed exit code: 0
```

Durations use exact compact formatting. The observed-exit line is omitted when unavailable. Quiet retains final
errors; plain/redirected output preserves words without styling. Framework diagnostics do not automatically expose
arguments or environment values, and recognized timeouts have no unexpected-exception stack trace.

## Verification

[Portable unit tests](../../tests/Rafter.UnitTests/Processes/ProcessTimeoutTests.cs) verify controlled event ordering,
late cancellation, exit zero/unknown metadata, output-only draining, blocked reporting, and settlement-failure precedence.
[Deadline arbitration tests](../../tests/Rafter.UnitTests/Processes/ProcessDeadlineTests.cs) exercise completion rechecks,
disarming, cancellation priority, single commitment, and omitted/maximum deadlines without timing-sensitive sleeps.
[Configuration tests](../../tests/Rafter.UnitTests/Processes/ProcessConfigurationTests.cs) cover immediate validation.

[Native Windows tests](../../tests/Rafter.WindowsTests/Processes/ProcessTimeoutTests.cs) verify owned children,
descendants after root exit, recovery, sticky failures, cancellation, and immutable launch settings. Existing
[observation tests](../../tests/Rafter.WindowsTests/Processes/ProcessObservationTests.cs) verify failed force requests
and combined termination uncertainty. Real [SDK consumers](../../tests/Rafter.WindowsTests/Consumers/ProcessApiConsumerTests.cs)
exercise both file apps and projects; [example checks](../../tests/Rafter.WindowsTests/Consumers/ProcessConsumerTests.cs)
run help, validation, a successful build, and an expired build deadline.

Use the [testing guide](../development/testing.md) for Windows and portable Linux profiles. Exact-commit CI results
belong to the delivery PR. Local Windows x64 / SDK 10.0.402 verification on 2026-10-22 passed **542 unit**,
**31 integration**, and **63 native/consumer Windows** cases, with one existing headless-console capability skip.
A subsequently added concurrent-scope isolation check also passed, bringing executed coverage to **637 passed,
0 failed, 1 skipped**. Release build has **0 warnings/errors**; all **23 redirected output** checks pass. Actual
pseudoterminal review confirms yellow deadline notices, red unhandled errors, and successful recovery under `NO_COLOR`
with emphasis retained. Raw local reports/captures are retained under `artifacts/process-timeouts/`. Portable tests establish
the configuration/supervision contracts without claiming Linux-native process ownership. Target, whole-invocation,
managed callback, and cleanup deadlines, additional analyzers, and native AOT remain separate work.
