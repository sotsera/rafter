# Release notes

## 0.1.0-preview.1 — prepared, not published

First preview of the rebuilt Rafter automation toolkit for .NET 10 and C# file-based applications.

- Commands with generated help, typed string/integer/Boolean/enum/duration inputs, validation, environment fallbacks,
  and sensitive string presentation masking.
- Sequential targets, dependency groups, conditional bodies, invocation-local state, and explicit target/command cleanup.
- Invocation roots and file/directory handles with explicit probes, directory creation, and UTF-8 text writes.
- Owned Windows child processes with descendant shutdown, cancellation, per-process timeouts, bounded capture,
  failure policies, reusable executables, and typed .NET/Git/npm helpers.
- Four authoring diagnostics, automatic namespace imports, packaged XML API documentation, and nineteen examples.

This preview is not API-compatible with the previous Rafter implementation. Within development of this rebuild,
`context.Executable(...)` replaced `context.Process(...)`; use the
[current executable guide](docs/guides/executables-and-dotnet.md) when adapting older examples.
Future previews may include reviewed breaking changes with migration notes; see the
[version and compatibility policy](docs/development/releases.md).

Native process ownership requires Windows. Portable Linux CI does not establish Linux-native ownership.
macOS and native AOT are unverified. Cleanup has no independent deadline and cannot launch child processes.
Sensitive inputs preserve raw value access; masking covers known exact values at Rafter presentation boundaries.
Direct console/file writes, child output, capture echo, and transformed/unregistered values remain outside protection.

Use the [local package instructions](docs/guides/local-package.md) to evaluate the prepared package before publication.
