# Rafter documentation

Start with the [repository README](../README.md) and [runnable examples](../examples/README.md).
For current status and priorities, use the [main development plan](design/example-design-plan.md#next-steps).
The [first milestone summary](history/milestone-review.md) records the completed Windows x64 baseline and its limits.

| Folder | Contents |
| --- | --- |
| [guides](guides/) | Current script-authoring and local-package behavior. |
| [design](design/) | Accepted decisions, open topics, and the illustrative output preview. |
| [development](development/) | SDK, workflow, skills setup, repository organization, and generated-file policy. |
| [history](history/) | Completed evaluations, captured samples, investigations, and milestone records. |

The two short pages at the old diagnostic/package paths preserve links embedded in existing packages.

## Using and authoring Rafter scripts

| Topic | Guide |
| --- | --- |
| Consume the local package, automatic imports, and opt-out | [Local package](guides/local-package.md) |
| Commands, bodies, invocation outcomes, and help | [Minimal runtime](guides/minimal-runtime.md) |
| Options, aliases, value lookup, and validation | [Typed inputs](guides/typed-inputs.md) |
| Environment fallbacks, empty values, precedence, and source diagnostics | [Environment inputs](guides/environment-inputs.md) |
| Enum choices, conversion, declaration validation, and allowed-values help | [Enum inputs](guides/enum-inputs.md) |
| Exact duration conversion, compounds, and canonical help defaults | [Duration inputs](guides/duration-inputs.md) |
| Invocation roots and child paths | [Roots and paths](guides/roots-and-paths.md), [agreed API design](design/roots-and-filesystem.md) |
| Multi-segment file/directory handles and explicit I/O | [Filesystem authoring](guides/filesystem.md), [design record](design/filesystem-authoring.md) |
| Sequential targets and dependencies | [Targets](guides/targets.md) |
| Conditional targets (verified locally) | [When/skip guide](guides/targets.md#conditional-target-bodies) and [agreed build/test/optional-pack example](design/example-design-plan.md#conditional-targets--agreed-behavior-2026-10-28) |
| Windows processes, failures, cancellation, and ownership | [Process runtime](guides/process-runtime.md) |
| Reusable executables, argument chunks, child overrides, and typed .NET build/publish/pack | [Executables and .NET](guides/executables-and-dotnet.md) |
| Typed Git status/log, npm install/CI/scripts, and custom tool adapters | [Git and npm](guides/git-and-npm.md) |
| Bounded process capture, ordered chunks, echo, and partial output | [Process capture](guides/process-capture.md) |
| Compiler/editor authoring diagnostics | [Authoring diagnostics](guides/authoring-diagnostics.md) |
| Human/plain output and help contracts | [Output design](design/output-design.md) |

The [output preview](design/output-preview.html) illustrates presentation choices; the
[runtime review samples](history/runtime-review-samples.md) and linked verification records capture actual executions.
The [environment-input design](design/environment-inputs.md) records the decisions behind the implemented
[environment-input API](guides/environment-inputs.md) and its example.
The [enum-input design](design/enum-inputs.md) records the agreed conversion, declaration-validation, and help rules
behind the [enum-input API](guides/enum-inputs.md) and [runnable example](../examples/enums.cs).
The [duration-input design](design/duration-inputs.md) records agreed authoring, help, conversion, and formatting rules
behind the [duration-input API](guides/duration-inputs.md) and [runnable example](../examples/durations.cs).
The [per-process timeout design](design/process-timeouts.md) and
[runnable example](../examples/process-timeout.cs) implement the agreed scope, recovery, exception/output contracts,
race arbitration, and configuration validation. See the [process timeout guide](guides/process-timeouts.md).
The [process capture design](design/process-capture.md) and [runnable example](../examples/process-capture.cs) deliver
`RunAndCaptureAsync(...)`, immutable streams/chunks, bounded Unicode-safe retention, and partial output on clean stops
for [issue #44](https://github.com/sotsera/rafter/issues/44). See the [capture guide](guides/process-capture.md) for usage,
recovery, output contracts, and verification scope. Public access to partial text on uncertain settlement is deferred.

## Developing this repository

Open the Git root containing `README.md`, `global.json`, and [Rafter.slnx](../Rafter.slnx). The enclosing workspace
directory is not itself the repository. The solution contains the two production projects, three xUnit/MTP suites
(unit, integration, Windows), and ProcessProbe. The other consumer and workflow projects remain isolated test/tool inputs;
their sample tests are exercised by integration scenarios rather than discovered as additional solution test suites.

Run from the repository root with the [pinned SDK and Windows baseline](development/sdk-and-platform-baseline.md):

```powershell
# Ordinary tests
dotnet test --solution Rafter.slnx --configuration Release --minimum-expected-tests 1

# Full Windows regression, including consumer/package and other long-running cases
dotnet test --solution Rafter.slnx --configuration Release -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1
```

Both commands use the repository's .NET 10 native Microsoft.Testing.Platform mode. To focus an individual
suite, consult the installed [run-tests skill](../.agents/skills/run-tests/SKILL.md). CI restores, builds, and tests
this same solution on Windows and the [portable filter](../Rafter.Portable.slnf) on Ubuntu. Exact commands and
evidence retention are in the [evaluation workflow](../.github/workflows/evaluation.yml). Package creation and manual/editor checks have
their own instructions in [local packaging](guides/local-package.md) and the [tool inventory](../tools/README.md).

| Development reference | Purpose |
| --- | --- |
| [GitHub workflow](development/github-workflow.md) | Upstream/fork identity, scoped issues, PRs, and merge conventions. |
| [Windows NuGet cache measurements](development/ci-cache-measurements.md) | Hosted comparison method, package-consumer isolation, measured results, and cache-placement decisions. |
| [CI duration and suite scheduling](development/ci-duration.md) | Full-suite profiling, Windows job boundaries, coverage manifests, and measured wall-time/runner-time tradeoffs. |
| [Skills setup](development/dotnet-skills-setup.md) | Installed guidance, provenance, workspace bridge, and updates. |
| [Repository organization review](development/repository-review.md) | Folder/project roles and the agreed next organization steps. |
| [Code review, 2026-10-23](development/code-review-2026-10-23.md) | Four correctness findings, fixes, regression evidence, and verification limits. |
| [Repository workflow](development/repository-workflow.md) | Maintained Windows file app for bounded metadata, build/test, optional packaging, explicit roots, and retained evidence. |
| [Development tools](../tools/README.md) | Current verification versus optional dependency evaluations, with reproduction commands. |
| [Generated files and evidence](development/generated-files.md) | Per-run evidence directories, CI retention, and bounded cleanup policy. |
| [Testing guide](development/testing.md) | Suite boundaries, ordinary/full/long-only profiles, capabilities, and observed selection checks. |
| [Test suite plan](development/test-suite-plan.md) | Agreed project split, execution-policy rationale, and separate Linux CI delivery. |

## Product direction and open work

The [example design plan](design/example-design-plan.md) owns accepted API decisions and the broader topic inventory.
The [first working examples record](history/first-working-examples.md) preserves the completed milestone's selected scope.
Later capabilities remain separate work; a completed example group does not establish every reference feature or
platform. Maintained test/tool inputs now live under [tests/testassets](../tests/testassets/README.md); its inventory
identifies automated and optional-tool callers. The three test projects separate in-process, portable integration,
and Windows responsibilities. Ubuntu 24.04 x64 now verifies the portable suite; Linux-native process ownership,
macOS, AOT, and other platform/environment checks retain their own acceptance gates.

## Evaluation and investigation history

These reports preserve evidence for their stated revisions. Their original commands, project names, and test counts
are historical snapshots; use the development entry points above for the current checkout.

| Record | Evidence and context |
| --- | --- |
| [Dependency decisions](history/dependency-decisions.md) | Initial shortlist and adoption gates. |
| [Evaluation harness](history/evaluation-harness.md) | Original SDK, test runner, and consumer scaffolding. |
| [Parser review](history/parser-review.md), [evaluation](history/parser-evaluation.md) | System.CommandLine source review and executable contract checks. |
| [Renderer review](history/renderer-review.md), [evaluation](history/renderer-evaluation.md) | Spectre.Console compatibility and comparison probes. |
| [Renderer measurements](history/renderer-measurements.md), [samples](history/renderer-samples.md) | Recorded startup/allocation observations and captures. |
| [Skills review](history/dotnet-skills-review.md) | Selection rationale for project-local skills. |
| [Aspire shutdown review](history/aspire-process-shutdown-review.md) | Upstream inspiration and process-lifetime considerations. |
| [Milestone review](history/milestone-review.md) | Completed baseline, terminal/editor observations, and verification limits. |
| [Process inheritance investigation](history/process-test-isolation.md) | Failed attempts, deterministic reproduction, and mixed-launcher fix. |

The previous implementation is preserved separately under [reference examples](../reference/examples/README.md)
and [reference output](../reference/output/README.md). Those snapshots describe the old project; some links in its
output notes intentionally depend on old files outside the preserved subset.
