# Sensitive inputs and redaction

Delivered in [PR #71](https://github.com/sotsera/rafter/pull/71), closing #70. All three PR checks and the
[merged-main evaluation](https://github.com/sotsera/rafter/actions/runs/40907617601) passed for `256edf9`.

Mark a string input with `.Sensitive()` to mask its known values when Rafter presents text:

```csharp
var token = command.Option<string>("token", "Example bearer token.")
    .FromEnvironment("RAFTER_TOKEN")
    .Sensitive()
    .Default("demo-token-only");

return await command.RunAsync(args, context =>
{
    var rawToken = context.GetValue(token);
    context.WriteLine($"Authorization: Bearer {rawToken}");
});
```

The script receives `demo-token-only`; stdout contains `Authorization: Bearer [REDACTED]`. The full
[redaction.cs example](../../examples/redaction.cs) prepares an HTTP request without sending it and demonstrates
an optional second token, environment fallback, validation, and a deliberately failing `--fail` path.

## Declaration and raw access

`.Sensitive()` works before or after `.Default(...)` and `.Required()`, returns the same handle type, and can be
repeated harmlessly. Only string inputs support it; other input types are declaration errors, including help.
Presence, aliases, source precedence, validation, and nullable optional-string access retain their existing behavior.

`GetValue(...)`, validators, conditions, work, and cleanup use raw strings. Actual HTTP headers, process arguments and
environment values, filesystem operations, and capture result objects are not rewritten. Exception objects remain
unchanged. Sensitivity is presentation metadata, without a wrapper type, reveal method, automatic disposal, or a
guarantee that managed strings are erased from memory.

## Protected presentation

Rafter masks known values in `context.WriteLine(...)`, help, lifecycle/process metadata, input diagnostics, and
exception details it renders. This includes state initialization and cleanup failures, inner exceptions, and fallback
error reporting after a renderer failure. Error scopes, invocation outcomes, quiet/plain/verbose modes, and stream
routing retain their existing rules. Quiet still keeps script stdout and failures; verbose never reveals the values.

Help marks sensitive inputs with `Sensitive.` and shows `Default: [REDACTED].` for every sensitive default, including
an empty one. Required and optional inputs have no invented default. Environment names and validation rules remain
visible unless their text itself matches a known value. Help reads no environment fallbacks and runs no validators,
state factories, predicates, bodies, or cleanup. Verbose source diagnostics still identify the source without values.

Each invocation snapshots sensitivity and owns its own redaction set, including during cleanup/final diagnostics.
Candidates are nonempty sensitive defaults (even when overridden), recognized supplied CLI values, and environment
values actually considered by normal resolution. They are collected before input-validation callbacks. Unused
environment variables are not read or registered. Repeated and overlapping invocations keep separate sets.

Matching uses exact ordinal, case-sensitive substrings. It selects the earliest match and the longest value starting
there, masks every occurrence, and never scans replacement markers again. Masking precedes wrapping, clipping, and
styling and crosses style boundaries in one framework message. Empty values add no matching pattern; short and
whitespace-only values may mask unrelated text. Ordinary text, whitespace, and literal stdout are otherwise retained.

## Boundaries

Direct `Console`/`TextWriter` output, external logging, files, debugger inspection, and OS command lines/history are
outside this presentation boundary. Live child stdout/stderr and automatic capture echo preserve raw child output.
Passing a sensitive input to a child does not protect what that child prints. Captured text stays raw until the script
explicitly writes it through `context.WriteLine(...)`.

Encoded, hashed, case-changed, truncated, transformed, and unregistered values are not discovered automatically.
Separate writes containing fragments of a value are not reassembled. Exception details may contain unrelated secrets
that were never registered. There is no global environment scan, streaming child-output redaction, CI-provider masking
command, secret-store integration, or new analyzer in this increment. Use synthetic tokens with the demonstration.

The [agreed contract and acceptance matrix](../design/example-design-plan.md#sensitive-inputs-and-redaction--agreed-behavior-2026-10-29)
record the selected scope. Implementation and local/hosted verification remain separate delivery checkpoints.

## Local verification, 2026-10-29

The full Windows regression passed **994 tests, 0 failures, 1 existing console-dependent skip** across 995 cases
(unit 811; integration 55; Windows 129). Clean Release solution/runtime-consumer builds and all 43 redirected runtime
checks passed. Thirty-eight new unit cases cover the public contracts and failure boundaries. The actual example
passed nine input/mode combinations plus help in both file-app and ordinary-project consumers. Fresh local-package
consumers verify raw access/masked presentation, and four native cases verify raw child inputs, capture, and echo.

Reports are under `artifacts/test-results/sensitive-inputs-full/`; retained logs and initial build errors are under
`artifacts/evidence/sensitive-inputs/`. PR/main checks, user review, and hosted
Linux verification remain delivery checkpoints; these results do not establish Linux-native ownership, macOS, AOT,
or attached-console/live-editor behavior.

Code review found no blocking runtime issues and added two regression rows for repeated primary/alias sensitive CLI
arguments. All **813 unit cases passed** afterward, including all 40 sensitive-input cases; runtime code is unchanged
from the full regression above. Review evidence is under `artifacts/evidence/sensitive-inputs/review/` and
`artifacts/test-results/sensitive-inputs-review-all-unit/`. The increment subsequently merged through #71;
its PR/main verification is linked at the top of this guide.
