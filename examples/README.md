# Rafter examples

The sixteen implemented examples listed below run against the new runtime through project references. The process examples currently require
Windows; the target-only example prepares and prints a greeting without child processes. See the
[design plan](../docs/design/example-design-plan.md) for accepted decisions and open topics.

Process examples use `context.Executable(...)` with grouped argument tokens. The repository workflow uses reusable
`context.Dotnet()` defaults and typed build/pack commands; see
[executables and typed .NET commands](../docs/guides/executables-and-dotnet.md).

`Directory.Build.targets` enables the same namespace import and analyzers as the
[local NuGet package](../docs/guides/local-package.md). The examples therefore omit `using Sotsera.Rafter;`. Set
`RafterImplicitUsings=false` to opt out and add the explicit import. When copying an example outside this checkout,
use a package reference with a configured local feed, or supply equivalent project-reference authoring assets.

| Example | Prerequisites |
| --- | --- |
| [minimal.cs](minimal.cs), [options.cs](options.cs), [validation.cs](validation.cs), [targets.cs](targets.cs) | .NET 10 SDK; use `--help` for declared inputs. |
| [environment.cs](environment.cs) | .NET 10 SDK; supply `--name` or `RAFTER_NAME`. Optional `RAFTER_REPEAT` and `RAFTER_UPPERCASE` provide fallbacks. See [environment inputs](../docs/guides/environment-inputs.md). |
| [enums.cs](enums.cs) | .NET 10 SDK; optional `--configuration` or `RAFTER_CONFIGURATION` selects Debug/Release, defaulting to Release. See [enum inputs](../docs/guides/enum-inputs.md). |
| [durations.cs](durations.cs) | .NET 10 SDK; optional `--delay` or `RAFTER_DELAY` supplies a nonnegative duration, defaulting to 30s; optional `--offset` accepts signed durations. See [duration inputs](../docs/guides/duration-inputs.md). |
| [processes.cs](processes.cs) | Windows and `dotnet` on the script process's executable search path. |
| [process-failure.cs](process-failure.cs) | `--project` identifies a buildable project; a failed build demonstrates `Fail` returning its result. |
| [process-working-directory.cs](process-working-directory.cs) | Launch from a directory containing `src` with a buildable project or solution. |
| [dependencies.cs](dependencies.cs) | `--project` identifies a test project; uses .NET 10 native Microsoft.Testing.Platform mode. |
| [process-timeout.cs](process-timeout.cs) | Windows; `--project` identifies a buildable project. Optional `--timeout` / `RAFTER_PROCESS_TIMEOUT` defaults to 10s. See [process timeouts](../docs/guides/process-timeouts.md). |
| [process-capture.cs](process-capture.cs) | Windows; captures its child's JSON. Optional `--echo`, `--show-order`, `--limit` (default 1024), and `--scenario Failure` / `Overflow` demonstrate capture behavior. See [process capture](../docs/guides/process-capture.md). |
| [repository-workflow.cs](repository-workflow.cs) | Windows and the pinned SDK; build/test this checkout, inspect bounded MSBuild JSON, and optionally `--pack`. `--root` supports another invocation directory. See the [workflow guide](../docs/development/repository-workflow.md). |
| [repository-status.cs](repository-status.cs) | Windows and Git on PATH; `--root` selects a repository with commits. Bounded status/log output, optional positive `--count`. |
| [frontend.cs](frontend.cs) | Windows and a supported Node/npm installation on PATH; `--root` selects a package.json directory. Runs `--script` (default build); `--install` explicitly opts into npm ci and requires a compatible lockfile. |

## Running the workflow examples

The [workflow fixture](../tests/testassets/workflows/) supplies a small library and a one-test project. From the repository root:

```powershell
dotnet run --file examples/process-failure.cs -- --project tests/testassets/workflows/src/BuildSample.csproj
dotnet run --file examples/dependencies.cs -- --project tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj
```

To exercise `process-working-directory.cs`, launch the file by its absolute path from `tests/testassets/workflows`.
The script location does not change the captured invocation root. The integration suite copies these fixtures into
temporary directories and also verifies intentionally broken builds and failing tests.

Use block lambdas for command and target bodies, even when the body contains one statement. Keep short predicates
and value-returning callbacks inline. Use `return;` only when a body needs to exit early, as in
[process-failure.cs](process-failure.cs).

Runnable [runtime consumers](../tests/testassets/consumers/runtime/) verify the implemented API in file apps and ordinary projects.
The separate [baseline consumers](../tests/testassets/consumers/host/) verify the SDK and consumer execution environment.
