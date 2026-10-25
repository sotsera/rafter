# Rafter roots and paths

Agreed design for the new Rafter. The [minimal runtime](minimal-runtime.md) implements per-invocation root capture;
the [process runtime](process-runtime.md) implements child working directories and environment overrides on Windows.
The first increment for [issue #46](https://github.com/sotsera/rafter/issues/46) adds public root/path access and
literal or prepared-option root selection, merged in [PR #50](https://github.com/sotsera/rafter/pull/50). The
[root/filesystem design](../design/roots-and-filesystem.md) records its agreed contract; richer filesystem helpers
are implemented separately under [issue #47](https://github.com/sotsera/rafter/issues/47).

## Default root

Without explicit root configuration, `Rafter.Command(description)` uses the process working directory at the start of `RunAsync` as the command root.
For an ordinary shell invocation, this is the directory from which the user launches the script.
The script's location does not determine the default root.

```csharp
var command = Rafter.Command("Print a greeting.");

return await command.RunAsync(args, context =>
{
    context.WriteLine("Hello from Rafter.");
});
```

The root is captured when `RunAsync` starts, rather than when `Rafter.Command(description)` is declared. Each invocation captures
its own root and retains that value for the run. A later working-directory change does not change the captured root.
If the script calls `Directory.SetCurrentDirectory(...)` before `RunAsync`, the default root is that new working
directory. This changes the script process's working directory, not the parent shell's directory.

For example, given a script at `C:\work\sample\scripts\hello.cs`:

| Working directory when launched | Default command root |
| --- | --- |
| `C:\work\sample` | `C:\work\sample` |
| `C:\work\sample\scripts` | `C:\work\sample\scripts` |
| `C:\scratch` | `C:\scratch` |

Moving the script alone does not change this rule. Launching it from another directory changes the default root.
Read the selected root through the read-only `context.RootDirectory` string property. It is stable for every body
and target in that invocation; reusing a command captures a new invocation directory for the next run.

## Path resolution and working directory

The command root is the base for relative paths resolved by Rafter. Rafter does not change the process working
directory to implement this behavior.

Ordinary .NET file operations and other libraries retain their own path-resolution behavior. For example, a direct
`File.ReadAllText("input.txt")` uses the process working directory; selecting a different Rafter root would not redirect
that call. Script code must explicitly resolve paths against the command root when that is its intent.

```csharp
var manifest = context.ResolvePath("artifacts/run.json");
await File.WriteAllTextAsync(manifest, text, context.CancellationToken);
```

`ResolvePath(string)` normalizes a relative path against the selected root or normalizes a fully qualified path
directly. `.` refers to the root and `..` is allowed. Resolution performs no filesystem I/O: it does not create a
directory, require existence, inspect links, or constrain paths to stay beneath the root. It returns an ordinary
absolute string for .NET APIs and tool arguments.

Null, blank, or NUL-containing paths are rejected. On Windows, drive-relative/rooted-but-not-fully-qualified forms
such as `C:output` and `\output` are rejected as ambiguous. These rejected forms throw an argument exception when
passed directly to the resolver; an unhandled exception follows the usual execution-failure contract. These rules do not change
ordinary .NET calls or the existing process working-directory/executable configuration APIs.

## Child process working directory

Child processes use the command root as their default working directory. `.WorkingDirectory(path)` overrides it for
that child: relative paths resolve against the command root, and absolute paths are used directly.

```csharp
await context.Process("dotnet")
    .WorkingDirectory("src")
    .Arguments("build")
    .RunAsync();
```

With a command root of `C:\work\sample`, this starts the child in `C:\work\sample\src`. The setting changes neither
the script process's working directory nor the parent shell's directory. It also leaves the command root unchanged.
See the [working-directory example](../../examples/process-working-directory.cs).

The process builder rejects a second `WorkingDirectory` call, even with the same path. An executable path containing
a separator or drive prefix also resolves against the captured root. Bare executable names use Windows direct-launch
lookup, including the script process's PATH; a child PATH override does not change executable lookup. Argument tokens
are passed unchanged, so relative paths inside arguments are interpreted by the child from its own working directory.

## Explicit roots

Select a literal root once when declaring the command:

```csharp
var command = Rafter.Command("Build a chosen repository.")
    .RootDirectory("../repository");
```

Relative configured roots resolve against the directory captured at `RunAsync` entry; absolute roots are normalized
directly. Root selection does not change the host CWD or probe whether the directory exists. The selected root becomes
the default directory for children and the base for `ResolvePath` and relative child-directory overrides.
Selected roots omit a trailing separator except when it is required for a filesystem root, so
`context.ResolvePath(".")` and `context.RootDirectory` have the same spelling.

A required/defaulted string option can instead supply the root:

```csharp
var command = Rafter.Command("Build this repository.");
var root = command.Option<string>("root", "Repository root relative to the invocation directory.").Default(".");
command.RootDirectory(root);

return await command.RunAsync(args, async context =>
{
    await context.Process("dotnet")
        .Arguments("build", context.ResolvePath("Rafter.slnx"))
        .RunAsync();
});
```

The option retains normal CLI/environment/default precedence, help, and validation. It is an ordinary declaration,
not a new built-in Rafter switch. Root selection reads the prepared value once after input validation and before
any body/target starts, using the original invocation directory even if a validator changed the host CWD.

Both overloads may be used only once in total. Repeats, null/blank/NUL-containing literal settings, null handles,
and handles owned by another command are initial declaration errors (exit `2`, including with help). Optional
`Option<string>` handles cannot be bound; use a required/defaulted `ValueOption<string>`.
Valid help does not resolve roots or probe directories. On an executing run, malformed selected root paths return
safe input/configuration diagnostics and exit `2`, without echoing rejected text. Nonexistent but lexically valid
roots are allowed; a later file operation or process launch decides whether an existing directory is required.

Source-directory discovery, repository discovery, and selector callbacks remain separate work. The
[filesystem helpers](filesystem.md) now build on this path base with immutable handles and explicit operations.
The [complete repository workflow](../../examples/repository-workflow.cs) uses that surface. Root verification is recorded in the
[design](../design/roots-and-filesystem.md).
