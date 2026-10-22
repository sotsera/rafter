# Enum inputs

Initial decisions agreed on 2026-10-22. The [enum example](../../examples/enums.cs) uses the current option API shape
for a build-configuration scenario inspired by the preserved
[option-types reference](../../reference/examples/option-types.cs). The initial conversion, declaration-validation, and
help rules below are agreed and implemented. The [current guide](../guides/enum-inputs.md) records verification.

## Agreed scope and conversion

- Add named enum choices to typed options, using a non-nullable enum type as `T` in `Option<T>`.
- Match supplied member names case-insensitively, for command-line and environment values. This does not change
  case-sensitive CLI option names or the platform-specific matching of environment-variable names.
- Accept member names only. Reject numeric text, even when the number is the underlying value of a declared member.
  For example, a member named `Debug` can be selected with `debug` or `DEBUG`, but not with its numeric value.
- Trim surrounding whitespace in command-line and environment values before matching a member name. Empty and
  whitespace-only values are input errors.
- Defer `[Flags]` enums from this first increment; do not introduce comma-separated or bitwise combinations.
- Keep the existing required/defaulted/optional handle contracts, input-source precedence, prepared `GetValue` values,
  validation timing, and source-aware diagnostics. Invalid supplied enum values are input errors, not a reason to fall
  back to an environment value or default.

The generic declaration shape is the same as existing options:

```csharp
var configuration = command.Option<BuildConfiguration>("configuration", "Build configuration.")
    .FromEnvironment("RAFTER_CONFIGURATION")
    .Default(BuildConfiguration.Release);
```

The example only reports the selected configuration. It does not build a project or launch a process, keeping
the enum authoring/conversion discussion separate from process execution. The enum is declared in the script:

```csharp
enum BuildConfiguration
{
    Debug,
    Release
}
```

## Agreed declaration validation and errors

- Reject enums containing member names that differ only by case, because case-insensitive matching is ambiguous.
- Reject multiple member names sharing the same numeric value, keeping defaults and displayed names unambiguous.
- Reject defaults that do not correspond to a declared member, such as `.Default((BuildConfiguration)99)`.
- Reject `[Flags]` enum declarations in this increment; flags support remains deferred.
- Report declaration problems during initial `RunAsync` validation, including before help is rendered.
- Report invalid supplied values through existing source-aware input diagnostics without echoing rejected text.

## Agreed help layout

Extend the existing option block with `<value>` as the placeholder and a separate `Allowed:` line, using declared
member spelling and declaration order. Put allowed choices after the description/policy and before environment metadata
and validation rules:

```text
  --configuration <value>
    Build configuration. Default: Release.
    Allowed: Debug, Release (case-insensitive).
    Environment: RAFTER_CONFIGURATION.
```

The default is the enum member name, without a numeric value or C# type qualification. Required and optional variants
would keep their existing policy wording instead. Long lists would wrap within the existing four-space metadata
gutter. Keep the existing blue option/environment names and lavender type hints; allowed choices can use ordinary
readable text. The block above is illustrative; the current guide records actual runtime verification.

The existing verbose `Input sources` layout can continue to describe the selected source without appending enum values.
Help should continue to use declaration metadata without reading mapped environment values or executing validators.

## Delivery

[Issue #38](https://github.com/sotsera/rafter/issues/38) tracks the agreed scope and verification through real
file-app/project consumers on Windows and Ubuntu. Implementation also rejects empty enum declarations because they
provide no named choice. Reflection reads public enum fields in metadata order to preserve C# declaration order,
including nonmonotonic numeric values, using the documented [.NET field-ordering contract](https://learn.microsoft.com/en-us/dotnet/api/system.type.getfields?view=net-10.0).
Duration options, configurable timeouts, enum-specific aliases/descriptions, and flags support remain separate topics.
