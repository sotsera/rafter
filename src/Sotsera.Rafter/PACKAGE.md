# Rafter by Sotsera

Rafter runs small C# automation scripts with descriptions, typed inputs, target dependencies, and owned child processes.
This is a local development package for the first working examples milestone; it has not been published to NuGet.org.

The runtime targets .NET 10. Verification targets SDK 10.0.402 on Windows x64 and Ubuntu 24.04 x64.
External process ownership is implemented on Windows; native AOT remains unverified.

Typed options can use `.FromEnvironment("NAME")` before or after `.Required()` / `.Default(...)`. Command-line values
take priority over environment values, then defaults. Help shows the mapping without reading values, and verbose
source diagnostics identify where prepared inputs came from without automatically printing their contents.
Named enum options use the same handles and source precedence, match member names case-insensitively after trimming
surrounding whitespace, and show allowed choices in help. Numeric text, flags, ambiguous names/values, and undefined
defaults are rejected during input or declaration validation.
Duration options use `Option<TimeSpan>` and accept explicit units (`250ms`, `30s`, `1h30m`) with exact conversion.
Help shows `<duration>`, accepted units, and normalized defaults. Parsing a duration does not impose an execution deadline.

```csharp
#:package Sotsera.Rafter@0.1.0-local
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
(RAF002), and rejects clearly discarded process tasks (RAF003). Runtime validation remains active when static analysis
cannot establish a declaration's value or when diagnostics are disabled. Roslyn and test tooling are not runtime dependencies.
