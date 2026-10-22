# Duration inputs

Initial decisions agreed on 2026-10-22. The [duration example](../../examples/durations.cs) is implemented through
`TimeSpan` options. The scenario is inspired by the preserved
[duration reference](../../reference/examples/durations.cs), using the current command and option API.
The authoring, help layout, conversion rules, compound-duration syntax, and default formatting below are agreed.
The [current guide](../guides/duration-inputs.md) records implementation behavior and verification.

## Agreed scope and authoring

- Add duration inputs through `command.Option<TimeSpan>(name, description)`, using the existing option API.
- Preserve optional, required, and defaulted handles, lookup nullability, validation, and invocation-local prepared values.
  Optional lookup returns `TimeSpan?`; required/defaulted lookup returns `TimeSpan` in both execution and input validation.
- Support `.FromEnvironment(...)` with the existing command-line > present environment > default precedence.
  Invalid selected input must not fall back to a lower-priority source.
- Start with explicit compact units, such as `250ms`, `30s`, `5m`, and `1h`. Reject bare numbers because their unit is unclear.
- Allow signed durations generally, including negative offsets. Apply `.Validate(...)` when a particular input must be
  nonnegative or satisfy another domain-specific rule; do not make nonnegative values a universal conversion restriction.
- Establish duration input conversion before designing configurable timeouts. A parsed duration alone does not create
  a timer, delay execution, cancel work, or impose a deadline.

The defaulted declaration follows the current API shape:

```csharp
var delay = command.Option<TimeSpan>("delay", "Delay to display.")
    .FromEnvironment("RAFTER_DELAY")
    .Default(TimeSpan.FromSeconds(30))
    .Validate("Must be zero or greater.", value => value >= TimeSpan.Zero);
```

The example also declares an optional signed `offset`, demonstrating omitted versus supplied durations. Its body only
prints prepared values. It explicitly uses invariant clock formatting for inspection; this is an example-body choice,
not a decision to accept clock input or use clock formatting in generated help.

## Agreed help layout

The placeholder is `<duration>`. The layout pairs it with a compact default and one `Units:` line before
environment metadata and validation rules. Keep the existing metadata indentation, wrapping, option/environment colors,
and lavender placeholder:

```text
  --delay <duration>
    Delay to display. Default: 30s.
    Units: ms, s, m, h.
    Environment: RAFTER_DELAY.
    Rules:
      - Must be zero or greater.

  --offset <duration>
    Signed offset to display. Optional; no default.
    Units: ms, s, m, h.
```

The units describe syntax rather than a finite set of allowed values, so help uses `Units:` rather than the
enum `Allowed:` label. Required and optional variants retain existing presence-policy wording. Actual option blocks
should retain existing spacing rules, including a blank line after an option with validation rules.
This transcript illustrates the agreed layout; captured verification belongs to the delivery record.

Continue the existing source-order explanation and value-free verbose `Input sources` block. Help should use declared
metadata without reading mapped environment values or running validators.

## Agreed compound-duration syntax

- Accept compound durations such as `1h30m`, `2m10s`, and `1s250ms` as well as single-component durations.
- Require components in largest-to-smallest unit order: hours, minutes, seconds, milliseconds. Units may be omitted,
  but each unit may appear only once.
- A single leading sign applies to the entire duration. For example, `-1h30m` means negative 90 minutes; component
  signs such as `1h-30m` are invalid.
- Only the final component may contain a fraction: `1h30.5m` is valid; `1.5h30m` is not.
- Do not require normalized component magnitudes. `1h90m` is valid and means 150 minutes.
- Exclude clock notation, such as `00:30:00`, from this first increment; explicit units describe the duration.

## Agreed conversion details

- Match `ms`, `s`, `m`, and `h` case-insensitively.
- Allow an explicit leading `+` as well as `-`, and use `.` as the invariant decimal separator. Require digits before
  the decimal point and, if present, after it.
- Trim surrounding whitespace for CLI and environment input; reject whitespace inside the number/unit expression.
- Reject empty/whitespace-only values, decimal commas, and exponents. Bare numbers remain rejected as already agreed.
- Reject overflow and values that cannot be represented exactly at `TimeSpan` precision, rather than rounding them.
- Apply the same conversion rules to CLI and environment text, with source-aware errors that do not echo rejected text.

## Agreed default formatting

Normalize help defaults into descending `h`, `m`, `s`, and `ms` components with no spaces, omitting zero components.
Use one leading minus sign for negative values and `0s` for zero. Hours may exceed 23, so day-length values still use
supported units. Keep fractional milliseconds only when required to preserve the exact value, using `.` and no
unnecessary trailing zeros. Do not round away precision.

| Duration | Help default |
| --- | --- |
| 30 seconds | `30s` |
| 90 seconds | `1m30s` |
| 1.5 hours | `1h30m` |
| 1.25 seconds | `1s250ms` |
| Negative 90 minutes | `-1h30m` |
| 25 hours | `25h` |
| Zero | `0s` |

Use this canonical spelling in help; accepting nonnormalized input does not require help to preserve its original text.

## Delivery

[Issue #40](https://github.com/sotsera/rafter/issues/40) tracks implementation and verification through real
file-app/project consumers, package consumers, and help on Windows and Ubuntu. Configurable process/managed timeouts
and their cancellation/settlement behavior remain separate topics.

The implementation uses BCL integer arithmetic to avoid floating-point or decimal rounding during conversion.
Each fraction is scaled to ticks and checked for a nonzero remainder using
[`BigInteger.DivRem`](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.biginteger.divrem?view=net-10.0),
then the total is checked against the signed tick range. No additional runtime package is required.
