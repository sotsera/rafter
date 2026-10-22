# Duration inputs

[Issue #40](https://github.com/sotsera/rafter/issues/40) adds exact duration inputs through the existing option API.
The [duration example](../../examples/durations.cs) follows the [agreed design](../design/duration-inputs.md):

```csharp
var delay = command.Option<TimeSpan>("delay", "Delay to display.")
    .FromEnvironment("RAFTER_DELAY")
    .Default(TimeSpan.FromSeconds(30))
    .Validate("Must be zero or greater.", value => value >= TimeSpan.Zero);
```

Use non-nullable `TimeSpan` as the supplied type. Optional lookup returns `TimeSpan?`; `.Required()` and `.Default(...)`
produce `ValueOption<TimeSpan>` with a non-nullable lookup. Execution and combined-validation contexts share these
contracts, including retained optional handles after finalization. Nullable supplied types fail declaration validation
before help. A duration input does not start a timer, delay a script, cancel work, or impose a deadline.

## Input syntax

Use case-insensitive `h`, `m`, `s`, and `ms` units. A duration contains one or more components in descending order,
without repeated units. A single optional leading `+` or `-` applies to the entire value. Only the final component may
contain a fraction; digits are required on both sides of a decimal point. Decimal `.` and digits `0` through `9` are
invariant under the current culture. Component magnitudes need not be normalized.

| Input | Meaning |
| --- | --- |
| `250ms` | 250 milliseconds |
| `1h30m` | 90 minutes |
| `-1h30m` | Negative 90 minutes |
| `1h90m` | 150 minutes |
| `1h30.5m` | 90 minutes, 30 seconds |
| `1s250ms` | 1.25 seconds |
| `0.0001ms` | One tick |
| `0s` | Zero, distinct from an omitted optional input |

Surrounding whitespace is ignored; internal whitespace is invalid. Bare numbers, empty/whitespace-only values,
decimal commas, exponents, clock forms, unknown/day units, repeated or ascending units, per-component signs, and
fractions before later components are rejected. `1.5h30m` is invalid even though `1h30.5m` is valid.

Conversion is exact across the full `TimeSpan` tick range, including `TimeSpan.MinValue`. Overflow and values between
ticks are errors, with no rounding or truncation. Trailing zeros do not reduce accepted precision. Negative values are
valid generally; option validators should enforce nonnegative or other domain restrictions where needed.

## Sources, validation, and diagnostics

CLI and environment values use the same conversion rules and existing CLI > present environment > default precedence.
Invalid selected input returns `2` without falling back or running validators/body. Errors identify the option and the
environment source where applicable, without echoing rejected text. Environment-variable names keep platform-native
case matching; this is independent of case-insensitive duration units.

`GetValue` reads the prepared invocation snapshot. It does not reread the environment or repeat conversion during
validation/execution; later invocations prepare fresh values. Existing individual and combined rules still apply to
converted values and defaults. The example uses a validator to reject negative delays while permitting signed offsets.

## Help and default formatting

```text
  --delay <duration>
    Delay to display. Default: 30s.
    Units: ms, s, m, h.
    Environment: RAFTER_DELAY.
    Rules:
      - Must be zero or greater.
```

The units line precedes environment metadata and validation rules. Help retains existing wrapping/gutters and terminal
colors, using the lavender `<duration>` placeholder. Plain/redirected output preserves information without styling.
Help does not read mapped environment values or run validators; invalid environment contents cannot block it.
Invalid declarations and malformed supplied CLI values still block help. Verbose source reporting remains value-free.

Help defaults normalize descending hour/minute/second/millisecond components, omit zero components, and use `0s`
for zero. Use one leading minus sign for negative values. Hours may exceed 23; fractional milliseconds retain exact
precision with no unnecessary trailing zeros. For example, 90 seconds displays as `1m30s`, negative 90 minutes as
`-1h30m`, and one tick as `0.0001ms`. A nonnormalized input such as `1h90m` therefore has canonical spelling `2h30m`.

The example body explicitly prints invariant clock-formatted values for inspection. That display choice does not
make clock text valid as input or change generated help formatting.

## Verification

[Unit tests](../../tests/Rafter.UnitTests/Inputs/DurationInputTests.cs) exercise single/compound/signed/fractional values,
grammar errors, precision/range boundaries, canonical formatting and deterministic round trips, invariant cultures,
typed handles, snapshots, source precedence, rules, privacy, and help layout/wrapping.
[Real consumers](../../tests/Rafter.IntegrationTests/Consumers/DurationConsumerTests.cs) verify both SDK hosts, actual
empty child environment values, full boundaries, and the runnable example. Existing
[package consumers](../../tests/Rafter.IntegrationTests/Consumers/PackageConsumerTests.cs) exercise packed duration input
behavior, and [compiler checks](../../tests/Rafter.IntegrationTests/Consumers/TypedInputConsumerTests.cs) reject assigning
optional durations to non-nullable locals.

Use the [testing guide](../development/testing.md) for Windows and portable Linux profiles. Exact-commit CI outcomes
belong to the delivery PR. On 2026-10-22, local Windows x64 / SDK 10.0.402 verification passed all **523 unit** and
**31 integration** cases. The full solution reports **605 passed, 0 failed, 1 skipped** across 606 cases; the skip is
the existing headless graceful-console capability case. Release build reports **0 warnings/errors** and all **23
redirected runtime output** checks pass. Actual pseudoterminal help review confirms blue names, lavender `<duration>`,
and NO_COLOR preserving emphasis without colors. Raw reports and captures are retained under `artifacts/duration-inputs/`;
these local results establish Windows behavior only.

Configurable process/managed timeouts, new cancellation/settlement policies, clock/day-unit
input, new authoring diagnostics, native AOT, and Linux-native process ownership remain separate work. No new runtime
package dependency is introduced.
