# Named enum inputs

[Issue #38](https://github.com/sotsera/rafter/issues/38) adds named enum choices to the existing option API. The
[enum example](../../examples/enums.cs) follows the [agreed design](../design/enum-inputs.md):

```csharp
var configuration = command.Option<BuildConfiguration>("configuration", "Build configuration.")
    .FromEnvironment("RAFTER_CONFIGURATION")
    .Default(BuildConfiguration.Release);
```

Declare the enum in the script or consumer project:

```csharp
enum BuildConfiguration
{
    Debug,
    Release
}
```

Use `Option<BuildConfiguration>` for an optional input. `GetValue` then returns `BuildConfiguration?`, with null
meaning omitted. `.Required()` and `.Default(...)` return `ValueOption<BuildConfiguration>`; lookup returns a
non-nullable `BuildConfiguration`. Execution and combined-validation contexts preserve these signatures, including
when the original optional handle is retained after finalization. Use a non-nullable enum type as the type argument.

## Conversion and validation

CLI and environment values select a declared member name using ordinal case-insensitive matching after trimming
surrounding whitespace. For example, `debug`, `DEBUG`, and ` Debug ` select `Debug`. Option names remain case-sensitive;
environment-variable names retain platform-native matching. CLI values override present environment values, then defaults.

Numeric text is invalid even if it equals a declared numeric value. Unknown names, empty or whitespace-only text,
qualified names (`BuildConfiguration.Debug`), quoted member names, and comma-separated combinations are invalid too.
Selected invalid values return exit `2` without falling back or running validators/body. Diagnostics identify the option
and environment source where applicable, without echoing rejected text.

Initial declaration validation returns `2` before help for `[Flags]` enums, empty enums, member names differing only by
case, multiple names sharing a numeric value, nullable enum type arguments, and defaults without a declared member.
All eight C# enum underlying integral types are supported; numeric values need not be contiguous or include zero.
Existing policy/mapping conflict checks and individual/combined validation apply unchanged.

Prepared values remain stable throughout validation and execution. `GetValue` does not reread the environment or
repeat conversion. Subsequent invocations prepare a fresh snapshot.

## Help and source reporting

```text
  --configuration <value>
    Build configuration. Default: Release.
    Allowed: Debug, Release (case-insensitive).
    Environment: RAFTER_CONFIGURATION.
```

Help uses `<value>`, a named default, and declared choice spelling/order, including when numeric values are out of
order. The `Allowed:` line precedes environment metadata and rules; long lists wrap in the four-space metadata gutter.
Existing option/environment colors and lavender placeholders apply in capable terminals. Descriptions and rules remain
literal text. Plain/redirected output preserves the information without styling.

Help does not read mapped environment variables or execute validators, so invalid environment contents do not block it.
Declaration errors and malformed supplied CLI values still block help. Verbose source reporting remains value-free;
it reports only command line, environment name, default, or not supplied.

## Verification

[Unit tests](../../tests/Rafter.UnitTests/Inputs/EnumInputTests.cs) cover conversion/rejection, all underlying types,
presence/default/retained handles, source precedence and snapshots, declaration errors before help, validator errors,
literal output, choice ordering, and wrapped metadata.
[Real consumers](../../tests/Rafter.IntegrationTests/Consumers/EnumConsumerTests.cs) exercise consumer-defined enums in
both file apps and projects, child environment overrides, safe errors, and the runnable example.
[Package consumers](../../tests/Rafter.IntegrationTests/Consumers/PackageConsumerTests.cs) verify named enum options
through a fresh local package in both hosts. The existing compiler checks also reject optional-enum assignments to
non-nullable locals.

On 2026-10-22, local Windows x64 / SDK 10.0.402 verification passed **431 unit** and **28 integration** cases. The full
solution reports **510 passed, 0 failed, 1 skipped** across 511 cases; the skip is the existing headless graceful-console
case in WindowsTests. Release build reports **0 warnings/errors**, and all **23 redirected runtime** review cases pass.
Actual pseudoterminal review confirms blue option/environment names, a lavender `<value>` placeholder, ordinary
allowed-choice text, and NO_COLOR retaining emphasis without colors. Raw reports, the initial test-assertion failures,
and terminal captures are retained under `artifacts/enum-inputs/`; redirected evidence uses the standard evidence tree.

Use the [testing guide](../development/testing.md) for the Windows and portable Linux profiles. Exact-commit CI outcomes
belong to the delivery PR; the local results above establish Windows behavior only.
No new runtime dependencies or analyzers are introduced. Flags, duration options, configurable timeouts, native AOT,
and Linux-native process ownership remain separate work.
