# Rafter by Sotsera

Rafter is a C# automation toolkit centered on small .NET 10 file-based applications. It provides typed inputs,
sequential targets, explicit cleanup, and owned Windows child processes, with generated help and authoring diagnostics.

The first preview, **0.1.0-preview.1**, is being prepared under
[issue #72](https://github.com/sotsera/rafter/issues/72). Build it locally using the
[package guide](docs/guides/local-package.md); public publication is a separate release step. Read the
[release notes](CHANGELOG.md) and [preview compatibility policy](docs/development/releases.md) before adopting it.

## Try an example

Install the stable .NET 10 SDK selected by [global.json](global.json), then run from this repository root:

```powershell
dotnet run --file examples/minimal.cs -- --help
dotnet run --file examples/options.cs -- --name Ada --repeat 2
dotnet run --file examples/redaction.cs -- --quiet
```

The [nineteen runnable examples](examples/README.md) use project references and automatically load the namespace
import and analyzers. Process examples require Windows and their listed external tools. For an independent file
app or ordinary project outside the checkout, follow the [fresh package consumer steps](docs/guides/local-package.md).

## Authoring capabilities

| Area | Current guides |
| --- | --- |
| Commands, help, validation, and typed values | [Runtime](docs/guides/minimal-runtime.md), [inputs](docs/guides/typed-inputs.md), [enums](docs/guides/enum-inputs.md), [durations](docs/guides/duration-inputs.md) |
| Environment fallbacks and sensitive string presentation | [Environment](docs/guides/environment-inputs.md), [sensitive inputs](docs/guides/sensitive-inputs.md) |
| Sequential targets, conditions, invocation-local state, and cleanup | [Targets](docs/guides/targets.md) |
| Invocation roots and explicit filesystem operations | [Roots and paths](docs/guides/roots-and-paths.md), [filesystem](docs/guides/filesystem.md) |
| Owned processes, timeouts, bounded capture, and reusable tool commands | [Processes](docs/guides/process-runtime.md), [timeouts](docs/guides/process-timeouts.md), [capture](docs/guides/process-capture.md), [.NET](docs/guides/executables-and-dotnet.md), [Git/npm](docs/guides/git-and-npm.md) |
| Compiler diagnostics, automatic imports, and API documentation | [Authoring diagnostics](docs/guides/authoring-diagnostics.md), [package](docs/guides/local-package.md) |

Sensitive input masking preserves raw value access and covers known exact values in Rafter's presentation.
Direct console/file writes, live child output, and capture echo remain outside that protection.
Cleanup is awaited after owned work settles, but has no independent deadline and cannot launch child processes.

## Platform and verification scope

Owned external processes require Windows. CI verifies portable commands, inputs, targets, and consumers on Windows
and Ubuntu 24.04 x64; portable Linux success does not establish Linux-native process ownership. macOS and native
AOT remain unverified. See [testing](docs/development/testing.md) and the
[SDK/platform baseline](docs/development/sdk-and-platform-baseline.md) for the precise boundaries.

Sensitive inputs merged in [PR #71](https://github.com/sotsera/rafter/pull/71); its three PR checks and
[merged-main evaluation](https://github.com/sotsera/rafter/actions/runs/40907617601) passed. Current priorities live in
the [development plan](docs/design/example-design-plan.md#next-steps). Earlier milestone test counts remain in the
[historical completion summary](docs/history/milestone-review.md), rather than describing the current inventory.

## Develop Rafter

Open [Rafter.slnx](Rafter.slnx) from this repository root. The two production projects contain the runtime and
 authoring analyzer; tests are split into unit, portable integration, and Windows suites. The
[repository workflow example](docs/development/repository-workflow.md) exercises build, tests, optional packaging,
and retained evidence. Use the [documentation index](docs/README.md), [verification tools](tools/README.md),
[contribution guidance](AGENTS.md), and [GitHub workflow](docs/development/github-workflow.md).

The previous implementation is preserved only as [reference examples](reference/examples/README.md) and
[output snapshots](reference/output/README.md); it is not a build dependency or a promise of API compatibility.
