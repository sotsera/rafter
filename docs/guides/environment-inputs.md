# Environment-backed inputs

[Issue #36](https://github.com/sotsera/rafter/issues/36) adds process-environment fallbacks to string/integer/Boolean
options. The [environment example](../../examples/environment.cs) uses all three types, following the
[agreed design](../design/environment-inputs.md).
The later [enum-input API](enum-inputs.md) uses the same fallback and source contracts for named enum choices.
The [duration-input API](duration-inputs.md) applies those contracts to exact explicit-unit `TimeSpan` values too.

```csharp
var name = command.Option<string>("name", "The person to greet.")
    .Required()
    .FromEnvironment("RAFTER_NAME");
```

`FromEnvironment` works before or after `.Required()` and `.Default(...)`, preserving lookup nullability and the
finalized handle's API constraints. Repeated mappings are invalid even when identical or configured through different
handles for the same option. Initial declaration validation returns `2` before help/lookup for conflicts or invalid names.

## Resolution and conversion

A supplied command-line option wins, followed by a present mapped environment variable, then the declared default.
Missing required inputs are invalid; missing optional inputs stay null. Invalid selected values return `2` without
trying a lower-priority source. Aliases identify the same option and value.

Names are preserved and matched by the platform: case-insensitive on Windows, case-sensitive on Linux. They cannot
be blank, contain `=` or control characters, or have surrounding whitespace. Uppercase is a portable convention,
not a restriction on other valid spellings.

Strings preserve all contents, including empty/whitespace-only text, quotes, and backslashes. Required means supplied;
use a validator to reject blank strings. Integers use invariant signed-decimal conversion with surrounding whitespace
ignored. Booleans ignore surrounding whitespace and accept only case-insensitive `true`/`false`. Empty/whitespace-only
integers or Booleans, numeric Boolean forms, yes/no/on/off, and quoted Boolean words are invalid. Environment text is
not parsed as command-line tokens; quotes and escapes are not interpreted.

Each invocation prepares values and source metadata before validation callbacks and execution. Selected variables are
read once per name using platform comparison. `GetValue` reads the prepared snapshot; later environment/declaration
changes do not change that invocation. Overlapping runs have independent snapshots. The library reads the current
process environment without modifying it or loading `.env` files.

## Help and source diagnostics

Help shows mappings, declared policies/defaults, and one source-order explanation without reading environment values
or running validators. Invalid environment contents cannot block help; declaration/malformed-CLI checks still apply.
Boolean help distinguishes command-line flags from environment true/false values.

After successful validation, `--verbose` writes one `Input sources` block to stderr before body/target execution.
It lists all options in declaration order using primary names, with `command line`, `environment NAME`, `default`, or
`not supplied`. Zero-length environment values get `(empty)` without warning coloring. Resolved contents are not
appended. Normal/quiet modes hide the block; failures retain grouped source-aware errors. Individual validator errors
identify environment sources. Author-supplied messages and unexpected exceptions retain existing policies; this is not
general secret redaction. Results stay on stdout; plain/redirected output preserves information without Rafter styling.

## Verification

[Unit cases](../../tests/Rafter.UnitTests/Inputs/EnvironmentInputTests.cs) inject lookup for selection, conversions,
invalid declarations, help without reads, stable/overlapping runs, validator diagnostics, and output modes.
[Real consumers](../../tests/Rafter.IntegrationTests/Consumers/EnvironmentConsumerTests.cs) exercise file apps and projects
with per-child environment overrides, including actual empty values, platform case behavior, and the runnable example.
[Fresh package consumers](../../tests/Rafter.IntegrationTests/Consumers/PackageConsumerTests.cs) consume the new API and
values through the packed runtime. These overrides do not mutate the test host's environment.

Use the [testing guide](../development/testing.md) for Windows and portable Linux profiles. The delivery PR records
exact-commit results and unavailable checks. New analyzers, async resolution, new option types, secret redaction,
and Linux-native process ownership remain separate work.

On 2026-10-22, local Windows verification passed all 399 unit cases, all 25 integration cases, and the Windows
process checks with the existing headless graceful-console skip. All 23 redirected runtime review cases passed.
Actual pseudoterminal review confirmed blue option/variable names, ordinary `(empty)` text, and NO_COLOR emphasis.
For runtime commit `0a07c5569e2124486c67c2d261cb0ee3cf37febf`, the
[Ubuntu CI job](https://github.com/sotsera/rafter/actions/runs/39815671192/job/119953501578) passed **400 ordinary** and
**424 full** portable cases with **0 failures/skips** and **0 build warnings/errors**. These runs include native
environment case matching and real empty process variables. Final PR results supersede these initial observations.
Raw reports, the first wrapping-assertion failure, and terminal records are retained under `artifacts/environment-inputs/`.
