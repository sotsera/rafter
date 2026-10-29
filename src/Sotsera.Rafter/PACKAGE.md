# Rafter by Sotsera

Rafter runs small C# automation scripts with descriptions, typed inputs, target dependencies, and owned child processes.
`ProcessCommand.RunAndCaptureAsync(...)` adds bounded decoded stdout/stderr, immutable ordered chunks, and optional
live echo. Its explicit positive limit counts UTF-16 code units per stream; clean overflow, timeout, and cancellation
exceptions expose bounded partial streams. Owned process execution currently requires Windows.
This is the first prepared preview of the rebuilt toolkit. See the
[release notes](https://github.com/sotsera/rafter/blob/main/CHANGELOG.md) and
[preview compatibility policy](https://github.com/sotsera/rafter/blob/main/docs/development/releases.md).
Consume this preparation from a local feed; publication is a separate release action.

Targets support `.When(description, context => ...)` with a synchronous Boolean predicate. A block lambda uses
an explicit `return`. Dependencies run first; false skips only the body and permits dependants to continue.
Help describes conditions without evaluating them, lifecycle output distinguishes skipped bodies, and quiet hides
routine skips. Conditions on groups, repeated conditions, and invalid descriptions/predicates are declaration errors.

`command.State(() => new MyState())` declares a typed key with reference state initialized separately for each
invocation after input/root preparation. `context.GetState(handle)` retrieves it in bodies and cleanup. State is not
automatically disposed. Described command/target `.Cleanup(...)` callbacks release explicitly acquired resources
after owned work settles, including failures/cancellation. Skipped targets do not clean up. Cleanup is awaited with
an uncancelled token and no child-process launch; independent errors are retained. Uncertain native termination
prevents unsafe resource cleanup and reports retention instead. Groups, stacked cleanup callbacks, and cleanup
deadlines remain outside this increment.

`context.Executable("tool")` replaces `context.Process("tool")`. Reusable executable settings are immutable;
each `.Arguments(...)` call on the executable creates a fresh command, and further calls append argument chunks.
Children can override defaults without changing siblings. `RemoveEnvironment(...)` explicitly removes child variables.
`context.Dotnet()` adds typed `Build`, `Publish`, and `Pack` commands with configuration/output options and a raw
`.Arguments(...)` escape hatch. `UseWorkingDirectorySdk()` explicitly removes inherited MSBuild/SDK selection
overrides without changing normal inheritance by default. Typed Git status/log and npm install/CI/scripts use the
same owned runner. npm launches Node plus npm-cli.js directly; package script shells remain npm's responsibility.

The runtime targets .NET 10. Verification targets SDK 10.0.402 on Windows x64 and Ubuntu 24.04 x64.
External process ownership is implemented on Windows; native AOT remains unverified.

Typed options can use `.FromEnvironment("NAME")` before or after `.Required()` / `.Default(...)`. Command-line values
take priority over environment values, then defaults. Help shows the mapping without reading values, and verbose
source diagnostics identify where prepared inputs came from without automatically printing their contents.
String inputs can use `.Sensitive()` before or after `.Required()` / `.Default(...)`. Value access remains raw;
help defaults, `context.WriteLine`, Rafter-authored metadata, and rendered exceptions mask known exact values with
`[REDACTED]`. Each invocation collects its own defaults, supplied CLI values, and considered environment fallbacks.
Direct console/file writes, live child output, and automatic capture echo remain raw. Transformed, fragmented, and
unregistered secrets are not discovered automatically; this is presentation masking, not a secret-storage facility.
Named enum options use the same handles and source precedence, match member names case-insensitively after trimming
surrounding whitespace, and show allowed choices in help. Numeric text, flags, ambiguous names/values, and undefined
defaults are rejected during input or declaration validation.
Duration options use `Option<TimeSpan>` and accept explicit units (`250ms`, `30s`, `1h30m`) with exact conversion.
Help shows `<duration>`, accepted units, and normalized defaults. Parsing a duration does not impose an execution deadline.
Process builders can set one positive `.Timeout(TimeSpan)` deadline covering the child and owned descendants.
Expiry initiates shutdown and output settlement, then throws `ProcessTimeoutException`; scripts can catch a cleanly
settled timeout for recovery. Unhandled timeouts return invocation exit `1`, separately from Ctrl+C cancellation.

```csharp
#:package Sotsera.Rafter@0.1.0-preview.1
#:property PublishAot=false

var command = Rafter.Command("Say hello.");
return await command.RunAsync(args, context =>
{
    context.WriteLine("Hello from Rafter.");
});
```

Restore from the local feed containing this package and NuGet.org for the runtime dependencies. See the repository's
[package instructions](https://github.com/sotsera/rafter/blob/main/docs/local-package.md) and
[diagnostic reference](https://github.com/sotsera/rafter/blob/main/docs/authoring-diagnostics.md).

Direct C# consumers receive an automatic `Sotsera.Rafter` namespace import. Set `RafterImplicitUsings=false` in
the project or a file-app property directive to opt out, then add `using Sotsera.Rafter;` explicitly.

The bundled C# analyzer rejects constant blank descriptions (RAF001), warns about nullable option type arguments
(RAF002), rejects clearly discarded process tasks (RAF003), and rejects repeated/conflicting option presence
policies in supported straight-line code (RAF004). XML API documentation accompanies the runtime for editor lookup.
Runtime validation remains active when static analysis
cannot establish a declaration's value or when diagnostics are disabled. Roslyn and test tooling are not runtime dependencies.
