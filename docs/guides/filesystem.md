# Filesystem authoring

The first filesystem increment merged in [PR #51](https://github.com/sotsera/rafter/pull/51) for
[issue #47](https://github.com/sotsera/rafter/issues/47).
It builds on the [root/path API](roots-and-paths.md). The [design record](../design/filesystem-authoring.md) owns
the agreed scope, behavior choices, and verification evidence.

## Select paths, then perform I/O

```csharp
var evidence = context.Directory("artifacts", "runs", runId)
    .EnsureExists();

await evidence.File("metadata.json")
    .WriteAllTextAsync(metadata, context.CancellationToken);

await context.Executable("dotnet")
    .Arguments("build", context.File("src", "Project.csproj").FullPath)
    .RunAsync();
```

`context.Directory(params string[] paths)` and `context.File(params string[] paths)` combine their segments with
`Path.Combine` and resolve against the selected command root. A `DirectoryPath` exposes the same two factories,
using its own `FullPath` as the base. There is no separate public `Combine` method. Selecting a handle does no I/O.

`DirectoryPath` and `FilePath` are immutable absolute path handles. Use `FullPath` explicitly for .NET APIs and
tool arguments. A handle does not hold an open stream or depend on a live invocation, so it can be shared between
targets or retained for later caller-owned work. It needs no disposal.

Factories require a non-null array with at least one nonblank segment; use `.` for the base directory. Null/NUL and
ambiguous Windows rooted segments are rejected before combination, even when a later absolute segment would
otherwise discard them. Fully qualified segments override earlier parts as with `Path.Combine`. `.`/`..` and
absolute paths follow the root resolver's lexical rules; no containment or link-resolution policy is added.
Directory handles normalize optional trailing separators while preserving filesystem roots.

## Explicit operations

| Operation | Behavior |
| --- | --- |
| `directory.Exists()` | Synchronous directory probe with .NET `Directory.Exists` semantics. |
| `file.Exists()` | Synchronous file probe with .NET `File.Exists` semantics. |
| `directory.EnsureExists()` | Synchronously create that directory and missing parents, return the same handle, and preserve an existing directory. A conflicting file fails normally. |
| `file.WriteAllTextAsync(contents, cancellationToken)` | Create or overwrite the file with exact UTF-8 text without a BOM or added newline; the parent directory must already exist. |

Creating a directory handle for a tool's output does not create that directory:

```csharp
var results = evidence.Directory("tests");
// Pass results.FullPath to the test runner; it creates its own result directory.
```

An empty string writes an empty file. Null text is rejected before creating or truncating a destination. A write
does not create missing parents. Existence probes do not reserve a path or guarantee later I/O will succeed.

## Cancellation and task ownership

Writes require an explicit token and must be awaited. The returned task completes after the underlying file
operation has settled and its stream is closed. The caller owns that task; the process runtime's automatic
unfinished-operation tracking does not apply to filesystem calls.

Pass `context.CancellationToken` for normal work. An explicit `CancellationToken.None` supports final evidence after
invocation cancellation:

```csharp
finally
{
    await evidence.File("run.json")
        .WriteAllTextAsync(manifest, CancellationToken.None);
}
```

Do not stop awaiting a write and assume timeout/cancellation has closed its stream. A precanceled write leaves
the destination untouched; cancellation or I/O failure after a write begins can leave partial/truncated contents.
Writes do not promise atomic replacement. Synchronous probes and directory creation are not advertised as
interruptible. I/O failures propagate through the usual body exception contract when unhandled.

The [complete repository workflow example](../../examples/repository-workflow.cs) uses these helpers. Its process setup/SDK-environment
follow-up remains separate. Delete/copy/globbing, stream factories, append, custom encodings, and JSON helpers
are not part of this first increment.
