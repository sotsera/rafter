# Rafter example design plan

This is the main development roadmap and record of example/API decisions. Start with the current position and next
steps below; the topic inventory and detailed decisions preserve the reasoning behind the implementation.

## Current position — 2026-10-28

Rafter has a working Windows-first automation runtime and a portable command/input/target layer. The **First working
examples** milestone is complete. Subsequent increments added environment inputs, named enums, exact durations,
per-process timeouts, bounded capture, invocation roots, filesystem helpers, separate suites, and portable Linux CI.

| Area | Implemented now | Remaining boundary |
| --- | --- | --- |
| Commands and inputs | Body callbacks, help, string/int/bool/enum/duration options, environment fallbacks, prepared values, and validation. | Further input types and asynchronous resolution remain separate design topics. |
| Targets | Sequential dependency graphs, groups, multiple selected entries, failure/cancellation handling, and locally verified conditional-body execution under #66. | Conditional delivery/user review remain pending; cleanup/shared-resource APIs remain undesigned. |
| Processes | Windows ownership, descendant cleanup, concurrent awaited operations, failure actions, timeouts, capture, reusable executables, typed .NET build/publish/pack, opt-in SDK environment isolation, and first Git/npm helpers merged through #65. | Further typed commands, Linux-native ownership, and broader deadlines remain separate work. |
| Paths and filesystem | Public roots, lexical resolution, multi-segment file/directory handles, explicit probes/directory creation/text writes, and child-directory overrides, merged through PRs #50/#51. | Source/repository discovery and broader filesystem operations remain separate work. |
| Authoring and packaging | Four analyzer diagnostics, automatic imports, runnable examples, and fresh local-package consumers. | Preview distribution, packaged XML API documentation, and API compatibility policy remain release work. |
| Verification | Unit, integration, Windows, package-consumer, and terminal/editor checks; Windows and Ubuntu CI. | Portable Linux results do not establish Linux process ownership. macOS and native AOT remain unverified. |

The focused delivery sequence is complete:

| Delivery | Status | Record |
| --- | --- | --- |
| Review fixes and verification tooling | Merged in [PR #49](https://github.com/sotsera/rafter/pull/49), closing #48. | [Four findings and evidence](../development/code-review-2026-10-23.md). |
| Invocation roots | Runtime/root consumers merged in [PR #50](https://github.com/sotsera/rafter/pull/50); workflow acceptance completed by #53, closing #46. | [Root contract and verification](roots-and-filesystem.md). |
| Filesystem helpers | Runtime/filesystem consumers merged in [PR #51](https://github.com/sotsera/rafter/pull/51); workflow acceptance completed by #53, closing #47. | [Filesystem contract and verification](filesystem-authoring.md). |
| Complete repository workflow and reconciled roadmap | Merged in [PR #53](https://github.com/sotsera/rafter/pull/53), closing #52/#46/#47. | [Workflow guide and independent verification](../development/repository-workflow.md). |
| CI triggers and supersession | Merged in [PR #55](https://github.com/sotsera/rafter/pull/55), closing #54. | [CI workflow record](../development/github-workflow.md#ci-efficiency-increment). |
| Windows cache measurement and package isolation | Merged in [PR #57](https://github.com/sotsera/rafter/pull/57), closing #56; default placement retained. | [Six-sample result](../development/ci-cache-measurements.md). |
| Parallel Windows evaluation | Merged in [PR #59](https://github.com/sotsera/rafter/pull/59), closing #58. | [Duration, coverage, and runner-time tradeoff](../development/ci-duration.md). |
| Reusable executables and typed .NET commands | Merged in [PR #61](https://github.com/sotsera/rafter/pull/61), closing #60; all three PR checks and main evaluation passed. | [Authoring and local verification](../guides/executables-and-dotnet.md). |
| SDK environment isolation and typed Git/npm helpers | Merged in [PR #65](https://github.com/sotsera/rafter/pull/65), closing #62/#63/#64; all three PR checks passed. | [SDK policy](../guides/executables-and-dotnet.md#selecting-the-sdk-for-the-child-directory) and [Git/npm verification](../guides/git-and-npm.md#local-verification-2026-10-27). |

PR #65 merged as `99358bb` on 2026-10-28. Its [PR evaluation](https://github.com/sotsera/rafter/actions/runs/40762862385)
passed Linux portable, Windows portable, and Windows native/packaging/output checks. The
[post-merge main evaluation](https://github.com/sotsera/rafter/actions/runs/40764786597) also passed.
Local verification for that delivery remains **866 passed, 0 failed,
1 console-dependent skip** across 867 cases, plus **43 redirected runtime cases**. Conditional execution below is
agreed and implemented locally under #66; those earlier results do not verify the new increment.

The post-merge [main CI run](https://github.com/sotsera/rafter/actions/runs/40221671404), for `d8e27a2`, passed Windows
full **806 tests, 0 failures, 1 console-dependent skip**, portable Linux full **710 tests, 0 failures, 0 skips**,
packaging, and **43 redirected runtime cases**. All five workflow consumers are included. The corrected PR head also
passed [its own CI](https://github.com/sotsera/rafter/actions/runs/40219933047); the workflow guide retains the initial
CI assertion failure and correction. Earlier results remain records of their original revisions.

The final workflow slice independently passed **806 tests, 0 failures, 1 console-dependent skip** on Windows
through the actual file app, created its local package, and passed an SDK-safe outside-checkout ordinary run
(**739 passed, 0 failed, 1 skipped**). Its five workflow consumers passed. The workflow guide retains exact
commands, run IDs, the initial deadline-consumer failure, and the scheduling adjustment before the passing full run.

## Next steps

**Deliver the reviewed conditional-target increment below through a PR.** Local review on 2026-10-28 found no
blocking code issues. Applicable hosted checks and user review remain delivery checkpoints. The user approved
`.When(...)` and its body-only skip semantics on 2026-10-28, including block lambdas with an explicit `return`.
Implementation is tracked by [#66](https://github.com/sotsera/rafter/issues/66), created before shipping-code changes.
The SDK-environment and Git/npm increment merged through #65 with all PR and main checks passing. Cleanup/shared
resources follow as a separate design topic. The complete workflow and deferred #46/#47 acceptance are merged.

CI trigger/concurrency changes merged through [PR #55](https://github.com/sotsera/rafter/pull/55), closing #54;
its post-merge main evaluation passed. The cache-placement measurement under
[#56](https://github.com/sotsera/rafter/issues/56) completed three successful samples per placement. Root restore
improved, but whole-job medians were 8m05s default and 9m03s runner-temp; keep default placement. The
[measurement record](../development/ci-cache-measurements.md) owns the method, source revision, samples, and limits.
The benchmark mode and fresh-package isolation fix merged in PR #57; its
[post-merge main run](https://github.com/sotsera/rafter/actions/runs/40403934136) passed. Profiling identified serial
portable integration and Windows-specific suites. The merged [scheduling increment](../development/ci-duration.md)
in PR #59 runs them on separate Windows runners, retaining all cases and
native isolation. Three complete hosted runs passed in 5m06s–6m00s (median 5m38s); the same-revision serial control
took 8m56s, and latest merged main took 7m00s. All 740 ordinary / 807 full Windows case identities and outcomes match.
The median summed Windows runner time increased to 9m49s versus 8m52s for the serial control. The scheduling record
preserves this tradeoff and measurement limits. Final PR verification passed all three checks in 5m46s. The
[executable and chunked-argument design](#process-authoring-draft-for-review) merged through #61 with all three PR
checks and its main evaluation passing. The [SDK/Git/npm increment](#sdk-isolation-and-gitnpm-helpers--selected-increment-2026-10-27)
merged through #65, closing [#62](https://github.com/sotsera/rafter/issues/62),
[#63](https://github.com/sotsera/rafter/issues/63), and [#64](https://github.com/sotsera/rafter/issues/64).
Remote archive caching and later product topics remain proposals.

| Order | Work | Intended outcome |
| --- | --- | --- |
| 1 | Deliver the [reviewed conditional-target increment](#conditional-targets--agreed-behavior-2026-10-28), issue #66. | Local review and full regression passed; PR CI and user review remain pending. |
| 2 | Reconcile the conditional delivery after merging. | Verify applicable PR/main checks and record the merged revision before marking the delivery complete. |
| 3 | Discuss cleanup/shared resources separately. | Use a concrete resource scenario to define acquisition, lifetime, cleanup eligibility, cancellation, and combined failures. |
| 4 | Define sensitive-value behavior before credential-heavy workflows. | Specify protection for defaults in help and framework diagnostics, and limits around script/child output and exception details. |
| 5 | Prepare a usable preview package and onboarding path. | Ship XML API documentation, choose preview versioning/release notes and compatibility checks, and verify a fresh consumer outside the checkout. |
| 6 | Plan Linux-native process ownership as a separate milestone. | Establish platform-specific launch, descendant, cancellation, timeout, capture, and settlement guarantees with real Linux tests. |

The CI review found duplicate fork-push/upstream-PR evaluations for #49. The merged change removes `build/**` push
evaluations and uses a workflow/event/PR concurrency group for PR runs. Main pushes and manual runs each use their
own run ID, so they neither cancel nor replace each other's pending runs. Its job contents and cache settings stayed
unchanged. The [CI workflow record](../development/github-workflow.md#ci-efficiency-increment) owns the details.
Benchmark cache placement separately from remote cache transfer; `NUGET_PACKAGES` overrides consumer NuGet
configuration, so isolated fresh-package checks must retain their own caches. Remote caching is not automatically
a win and no lock-file policy has been adopted.

Keep the two production projects and existing ownership boundaries. Additional layers, parallel target scheduling,
more input types, machine-readable output, cross-target process settings, macOS, and AOT should follow a concrete use case
and their own design/verification decisions. The order above can change when the workflow reveals a stronger need.

### Fix backlog from the code review

All entries are **merged through PR #49**, closing issue #48. The IDs below are local review
identifiers, not GitHub issue numbers. Detailed evidence, regression coverage, and verification limits live in the
[review record](../development/code-review-2026-10-23.md); link that record from future issues rather than duplicating it.

| ID | Priority | Implemented fix | Regression coverage |
| --- | --- | --- | --- |
| R1 | P2 | Remove settled process tasks/results from the body tracker; retain ownership of unfinished operations. | Real captures become collectible while the body and builder remain alive; existing abandonment and shutdown coverage remains. |
| R2 | P3 | Validate independently convertible CLI options while excluding structurally invalid parser results. | Invalid enum/duration combinations, reversed declaration order, and unrelated structural errors report together without exposing values or running callbacks. |
| R3 | P2 | Share physical terminal line state and serialization across stdout/stderr; keep redirected destinations independent. | Both stream directions, rich/plain output, partial lines, and all redirection combinations. |
| R4 | P2 | Supervise process exit and output drain together; cancel and join owned reads, bound root cleanup, and retain workspaces/evidence on uncertain descendant cleanup. | Real inherited-pipe descendant reproduction, live-root timeout/cancellation, exact/large output, and retained consumer diagnostics. This helper does not provide native descendant containment. |

P2 means a normal-priority correctness/reliability fix; P3 means a lower-priority diagnostic/usability fix. These
priorities order follow-up work and do not claim an emergency or a completed release-readiness assessment.

### Delivered increment: one complete automation workflow

[repository-workflow.cs](../../examples/repository-workflow.cs) is the maintained Windows-only file-app baseline.
It uses the merged root/filesystem APIs with ordinary C# sequencing, `if (pack)`, and
`try/finally`. The [workflow guide](../development/repository-workflow.md) owns usage, evidence paths, independent
verification, and the SDK-launch caveat. Its five real consumer scenarios use a small solution rather than recursively
running the repository's full workflow tests.

Acceptance for this delivery:

- Inspect bounded MSBuild JSON, build/test the selected root, and optionally create a local package.
- Help and invalid inputs create no workflow evidence; default and explicit roots are supported without changing CWD.
- Build/test failures prevent later stages. Timeouts settle owned process work before the final manifest write.
- Preserve completed stages, logs, metadata, TRX, and package paths in a unique evidence directory on success/failure.
- Run the actual repository with full tests and packaging, plus an SDK-safe outside-checkout launch.

The existing body/target/process APIs and public root/filesystem helpers are the agreed foundation. `if (pack)`
is sufficient for this workflow; target conditions do not acquire a new requirement from this example. Sensitive
values, public publication, and Linux-native ownership remain separate work.

### Workflow cleanup follow-up

Revisit [repository-workflow.cs](../../examples/repository-workflow.cs) as process-authoring APIs evolve. Its
original Dotnet helper remains in the before example below for comparison, with these follow-ups:

- #60 merged in #61, replacing repeated process configuration with `context.Dotnet()` and typed build/pack calls.
  All five migrated workflow scenarios and both platforms' applicable PR/main checks passed.
- #62 merged in #65, adding explicit `.UseWorkingDirectorySdk()` to remove inherited SDK/MSBuild overrides from
  children. The guide distinguishes controlled mixed-SDK reproduction from the current natural preview launch;
  choosing the outer file-app SDK remains separate.
- The first root/filesystem cleanup is implemented through #50/#51 and used by this example: directory/file
  composition, probes, evidence creation, and metadata/manifest writes use the agreed helpers.
- Rerun workflow success, failure, timeout, root, optional-pack, and evidence checks after future cleanup. Preserve
  process/stream settlement and explicit cancellation choices for final evidence.

The merged #62 increment adds an explicit SDK policy and opts this workflow in. The
[design below](#process-authoring-draft-for-review) preserves the original syntax discussion and selected follow-up.

### Process authoring draft for review

**Status: syntax and typed-helper direction agreed, 2026-10-27; merged in
[PR #61](https://github.com/sotsera/rafter/pull/61), closing [#60](https://github.com/sotsera/rafter/issues/60).** Use
`context.Executable("dotnet")` for reusable executable configuration and repeated `.Arguments(...)` calls to group
one, two, or many separate tokens. The same calls can form one inline chain for a single invocation. This replaces
the earlier `ProcessTemplate(...).Create(...)` proposal. The merged runtime provides `Executable`, explicit
`RemoveEnvironment`, and the first typed .NET helpers. The SDK/Git/npm extension is described separately below.
The maintained [repository workflow](../../examples/repository-workflow.cs) remains the runnable baseline.

The immediate use case is one workflow body launching several independent `dotnet` children with the same environment
and per-process timeout. Keep option resolution explicit through `context.GetValue(timeout)` after normal input
validation. The excerpts use the existing body's `context`, `project`, `solution`, `evidence`, and
`buildConfiguration` variables; JSON parsing, test arguments, optional packaging, and final-manifest writes retain
their existing sequencing.

#### Before: previous process factory

Before this increment, the workflow used this local C# function to create a fresh configured child for each operation:

```csharp
ProcessCommand Dotnet(params string[] arguments)
{
    return context.Process("dotnet")
        .Environment("DOTNET_NOLOGO", "1")
        .Environment("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
        .Environment("DOTNET_CLI_USE_MSBUILD_SERVER", "0")
        .Timeout(context.GetValue(timeout))
        .Arguments(arguments);
}

var metadata = await Dotnet("msbuild", project.FullPath, "-nologo",
        "-getProperty:PackageId,Version,TargetFramework", "-property:Configuration=" + buildConfiguration)
    .RunAndCaptureAsync(maxCharactersPerStream: 16 * 1024);

await Dotnet("build", solution.FullPath, "--configuration", buildConfiguration,
        "--disable-build-servers", "-m:1",
        "-flp:LogFile=" + evidence.File("build.log").FullPath + ";Verbosity=normal")
    .RunAsync();
```

The helper gives each operation a fresh builder. `ProcessCommand.Arguments(...)` appends to that builder;
keeping one builder and appending the next command's arguments would accumulate arguments. Each execution snapshots
the current configuration; the builder is not a reusable argument factory.

#### After: agreed executable and argument syntax

Configure a reusable executable, then group arguments by meaning for each child command:

```csharp
var dotnet = context.Executable("dotnet")
    .Environment("DOTNET_NOLOGO", "1")
    .Environment("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
    .Environment("DOTNET_CLI_USE_MSBUILD_SERVER", "0")
    .Timeout(context.GetValue(timeout));

var metadata = await dotnet
    .Arguments("msbuild", project.FullPath)
    .Arguments("-nologo")
    .Arguments("-getProperty:PackageId,Version,TargetFramework")
    .Arguments("-property:Configuration=" + buildConfiguration)
    .RunAndCaptureAsync(maxCharactersPerStream: 16 * 1024);

await dotnet
    .Arguments("build", solution.FullPath)
    .Arguments("--configuration", buildConfiguration)
    .Arguments("--disable-build-servers", "-m:1")
    .Arguments("-flp:LogFile=" + evidence.File("build.log").FullPath + ";Verbosity=normal")
    .RunAsync();
```

The same entry point works inline without a reusable local variable:

```csharp
await context.Executable("dotnet")
    .Arguments("build", solution.FullPath)
    .Arguments("--configuration", buildConfiguration)
    .Arguments("--disable-build-servers", "-m:1")
    .RunAsync();
```

`context.Executable("dotnet")` returns an immutable `Executable` bound to the current
body. Its fluent settings return a new executable configuration. `Executable.Arguments(params string[] arguments)`
returns a fresh `ProcessCommand` with copied defaults and argument tokens. Subsequent `ProcessCommand.Arguments(...)`
calls append to that command using the existing behavior. Neither configuration nor argument calls launch a child.
`RunAsync` or `RunAndCaptureAsync` starts it using the existing ownership and settlement rules. The executable retains
configured values, not past operations, results, captures, or output streams.

An option/value pair is two ordinary tokens in one call, not a new parser or automatic option formatting feature.
One-token forms such as `-property:Configuration=Release` remain one token. Keep the existing support for empty
string tokens, reject null/NUL tokens, and copy caller arrays before retaining them.

The executable settings are environment edits, timeout, and an optional working directory. The normal
default directory stays `context.RootDirectory`. Accepted exit codes and failure handlers remain child settings;
capture bounds and echo remain execution choices at the call site, as in the current API.

#### Per-child changes and precedence

A child can explicitly override an executable default once without changing the next child:

```csharp
var probe = dotnet.Arguments("--version")
    .Timeout(TimeSpan.FromSeconds(30));

var sdk = await probe.RunAndCaptureAsync(maxCharactersPerStream: 1024);
context.WriteLine(sdk.StandardOutput.Text.Trim());

// A later dotnet.Arguments(...) still has the executable's configured timeout.
```

| Setting or operation | Proposed behavior for review |
| --- | --- |
| Environment | Inherit the host environment normally, apply executable edits, then explicit child edits. Copy configured edits; do not freeze the whole inherited environment when configuring the executable. |
| Timeout | Child value overrides the executable value. It is a default, not an invocation-wide maximum. Existing positive-duration validation and shutdown/settlement semantics apply. |
| Working directory | Invocation root, then optional executable directory, then explicit child directory. Resolve relative paths against the invocation root without changing host CWD. |
| Duplicate settings | Each setting/name may be written once within an executable configuration chain and once within a child chain. A child override is a separate layer; a second explicit child write remains an error. |
| Arguments | Every call on `Executable` starts a fresh `ProcessCommand`. Calls on that command append only to it, retaining the current token/no-shell contract. |
| Failure and output | Keep default rejected-exit throwing, child-specific handlers, bounded capture, raw child streams, and explicit awaiting. Configuring an executable changes none of these policies. |
| Lifetime | Executable and children belong to the originating body. They cannot launch after it ends, keep it open implicitly, or silently bind to another target/body. Configuring an executable/child does not start an operation. |

Independent children can still run concurrently when explicitly awaited together. A child override must not leak into
the executable, another child, or the parent environment. An executable timeout does not replace invocation cancellation.
If a command option must act as a hard upper limit rather than a default, that is a separate policy to agree.

#### SDK environment: explicit candidate, not a verified fix

This subsection preserves the #60 review candidate. The later #62 investigation below reproduces mixed targets and
selects an explicit `UseWorkingDirectorySdk()` policy; use that section for current implementation status.

`RemoveEnvironment(params string[] names)` is implemented on both the executable configuration and a child command. Removal means absence
in that child's environment block, distinct from setting an empty string. A variable set or removed explicitly by
the child takes precedence over an executable edit of the same name; conflicting edits within one layer are rejected.

An illustrative SDK-specific derivation would be:

```csharp
// Candidate only: the complete removal list must come from reproducing the recorded failure.
var standaloneDotnet = dotnet.RemoveEnvironment("MSBuildSDKsPath");

await standaloneDotnet
    .Arguments("build", solution.FullPath)
    .Arguments("--configuration", buildConfiguration)
    .Arguments("--disable-build-servers", "-m:1")
    .RunAsync();
```

The original `dotnet` configuration would still inherit that variable; the derived executable opts out for its children.
Microsoft documents [MSBuildSDKsPath](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties)
as an SDK lookup path. That makes it a candidate to investigate, not proof that removing this one variable fixes
the [recorded outside-launch failure](../development/repository-workflow.md#verification-after-rootpath-cleanup).
Inventory the actual inherited SDK/MSBuild variables and verify a real metadata/build operation before choosing a
removal list. A successful `dotnet --version` alone would not establish that MSBuild loaded the correct SDK/tasks.

Keep normal environment inheritance for generic processes. Preserve unrelated settings such as `PATH`, proxy and
NuGet cache configuration. Do not automatically sanitize every executable named `dotnet` or remove all `DOTNET_*`
variables: Microsoft gives [DOTNET_HOST_PATH](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables)
a deliberate role for SDK-launched tools. A higher-level dotnet helper needs its own explicit environment policy.

This proposal concerns children launched by Rafter. The SDK running the outer file app is selected before Rafter
handles `--root`; child environment edits cannot retroactively change that host. The existing pinned-SDK/built-DLL
launch workaround remains the verified approach until the new behavior is implemented and tested.

#### Typed command helpers — agreed direction, 2026-10-27

The #60 increment established typed helpers on the generic executable API, starting with .NET. Git/npm are the
current #63/#64 follow-ups described below. Helpers compose `Executable` and `ProcessCommand` and translate named methods into
argument tokens. They must use the same launch, capture, failure, cancellation, timeout, and settlement pipeline.
Retain `.Arguments(params string[])` as an escape hatch on both the reusable tool and each typed command.

```csharp
var dotnet = context.Dotnet()
    .Timeout(TimeSpan.FromMinutes(5));

await dotnet.Build(solution.FullPath)
    .Configuration("Release")
    .NoRestore()
    .RunAsync();

await dotnet.Publish(project.FullPath)
    .Configuration("Release")
    .Output(output.FullPath)
    .RunAsync();
```

For this increment, add reusable `DotnetExecutable` plus distinct build, publish, and pack command types. Provide
command-appropriate configuration/output options, no-restore/no-build options where valid,
and build-server/max-CPU settings needed by the maintained workflow. MSBuild properties and file logger settings use
the raw argument escape hatch in this first increment. Keep configuration names as strings
to support custom build configurations. Raw MSBuild inspection and repository-specific native-MTP test arguments
remain grouped `.Arguments(...)` calls; do not invent a universal typed test-runner policy in this increment.

Keep the tool-specific implementation in its own source folder, with no new dependencies or package/project split.
The typed adapter is a thin composition layer that can be extracted later; generic ownership remains in the runtime.
Future Git/npm adapters should follow the same composition pattern after their concrete command syntax is reviewed.

Implementation scope selected for the generic foundation: body-bound immutable executable settings; one explicit
child override per setting/name; zero-argument execution through fresh commands; explicit child-only environment
removal, with no automatic dotnet sanitization. Replace `context.Process(...)` in maintained callers. The recorded
outside-launch SDK failure remains a separate investigation until a real reproduction establishes its policy.

#### Generic implementation structure

The entry-point name, chunked argument calls, and inline/reusable authoring forms are agreed. Implement them through
the existing process pipeline rather than adding another launcher:

1. Add the public `Executable` configuration type and `RafterContext.Executable(string executable)` entry point.
   Share validation with `ProcessCommand`; use defensive copies for configuration collections.
2. Keep executable defaults separate from explicit child settings. Copying defaults into today's single-assignment
   fields would incorrectly make a valid child override look like a duplicate write. Merge the two layers into the
   existing `ProcessSettings` snapshot immediately before execution.
3. Keep `ProcessCommand` as the invocation builder with its existing chunked arguments, failure handlers, accepted
   exit codes, and run/capture methods. Preserve its mutable append behavior and execution snapshots; saving a
   `ProcessCommand` does not turn it into a reusable argument factory.
4. Reuse `BodyProcesses`, `ProcessOperation`, and the native runner for execution and cleanup. Add no alternate
   launch, stream, cancellation, timeout, or settlement path for reusable executables.
5. Replace the current `context.Process(...)` entry point with `context.Executable(...)` in maintained examples,
   consumers, tests, and current API guides. Preserve historical records and this before example. Do not retain an
   alias by default; record the source-breaking rename. Replace the workflow's local Dotnet helper with the agreed
   reusable configuration and grouped arguments.

Selected scope for #60:

1. **Lifetime and reuse scope.** Configuration is body-bound. Sharing outside the executing body or across targets
   remains separate work. Input-dependent settings resolve explicitly from context; no `.Timeout(option)` binding.
2. **Overrides.** Child settings take precedence over executable defaults; duplicate explicit writes within each
   layer are rejected. This is distinct from two writes on today's command builder.
3. **Zero-argument execution.** `Executable.RunAsync()` and `RunAndCaptureAsync(...)` forward through a fresh
   zero-argument `ProcessCommand` each time. Both forms use the same pipeline and ownership rules.
4. **Environment removal.** Explicit child-only removal is in scope. Automatic .NET-specific sanitization and a
   verified SDK variable list remain follow-ups requiring reproduction.

Issue #60 was created before code implementation. See the
[authoring guide](../guides/executables-and-dotnet.md) for the local API and migration details.
Acceptance should exercise argument grouping/order, caller-array isolation, reusable and inline calls, zero-argument
execution, independent commands and overrides, concurrent children, body lifetime, unset versus empty variables,
unchanged parent environment, help and invalid-input nonexecution, and actual inside/outside-checkout SDK selection
plus metadata/build behavior. SDK-policy verification remains a follow-up rather than a claim of this increment. Then
replace the workflow helper and rerun its success, build/test failure, timeout, optional-pack, and final-evidence
scenarios. Local Windows verification is recorded in the
[authoring guide](../guides/executables-and-dotnet.md#local-verification-2026-10-27): **830 passed, 0 failed,
1 console-dependent skip**, fresh package consumers passed, and **43 redirected runtime cases** passed. The final
Release build had no warnings/errors. #61 subsequently passed all applicable PR and main CI checks and merged.
Attached-console and live-editor checks were not run for that increment.

### SDK isolation and Git/npm helpers — selected increment, 2026-10-27

The user authorized the SDK inheritance fix and the first Git/npm helpers after #61 merged. Issues
[#62](https://github.com/sotsera/rafter/issues/62), [#63](https://github.com/sotsera/rafter/issues/63), and
[#64](https://github.com/sotsera/rafter/issues/64) were created before shipping-code changes. Local review completed
on 2026-10-28 with no blocking code issues. [PR #65](https://github.com/sotsera/rafter/pull/65) merged after all three PR
checks passed. The delivery uses the existing process ownership pipeline; the unrelated `examples/environment.cs`
working-tree state was excluded and remains preserved.

The installed 11.0.100-rc.3 preview no longer reproduces the earlier natural outside-launch injection: all inspected
SDK variables were absent and a child resource build selected 10.0.403 successfully. A controlled host carrying
`MSBuildSDKsPath` from the installed preview does reproduce the recorded **MSB4216/MSB4027** errors: SDK 11 targets
load under MSBuild 10. Keep both observations and their logs; do not claim the current preview still injects them.

Selected authoring shape:

```csharp
var dotnet = context.Dotnet()
    .UseWorkingDirectorySdk()
    .Timeout(TimeSpan.FromMinutes(5));
await dotnet.Build(solution.FullPath).Configuration("Release").RunAsync();

var git = context.Git().WorkingDirectory(repository.FullPath);
var status = await git.Status().Short().Branch().RunAndCaptureAsync(4096);
var log = await git.Log().MaxCount(5).Oneline().RunAndCaptureAsync(4096);

var npm = context.Npm().WorkingDirectory(frontend.FullPath);
await npm.Ci().IgnoreScripts().RunAsync();
await npm.RunScript("build").ScriptArguments("--mode", "production").RunAsync();
```

`UseWorkingDirectorySdk()` is explicit opt-in configuration, not a change to generic `Executable` or default
`Dotnet()` inheritance. Remove inherited SDK/MSBuild selection overrides (`MSBuildSDKsPath`, `MSBUILD_EXE_PATH`,
`MSBuildExtensionsPath`, and the three documented `DOTNET_MSBUILD_SDK_RESOLVER_*` variables). Preserve `DOTNET_HOST_PATH`,
`DOTNET_ROOT*`, PATH, proxies, NuGet settings, and other normal variables. The working directory/global.json still
chooses the SDK; the policy does not pin one or retroactively choose the outer file-app SDK. Explicit command-level
environment edits can override removals; duplicate writes within one executable/command layer retain their errors.
The repository workflow opts in. Cross-SDK reproduction and stable invalid-path regressions verify real
metadata/build behavior, parent/sibling isolation, and root/directory behavior.

Git starts with `Status()` (`Short`, `Branch`, `Porcelain`) and `Log()` (`MaxCount`, `Oneline`) plus raw `Arguments`.
These helpers do not parse results, add shell execution, or contact a remote by themselves. Defer typed mutations,
credentials, and network commands until demonstrated by another workflow.

npm starts with `Install()`, `Ci()`, and `RunScript(name)`, common no-audit/no-fund/ignore-scripts options for install
commands, and script-specific `IfPresent()`/`ScriptArguments(params string[])`. Script argument chunks add the `--`
boundary once and retain token order. npm options must precede that boundary. Raw `Arguments` remains available.
On Windows, execute Node with an explicit npm-cli.js path rather than invoking npm.cmd through a shell. Discover the
standard paired Node/npm installation on PATH, or accept explicit Node/CLI paths for other managers. Document the
supported layout and clear discovery failures. npm itself owns package-script shell behavior; do not claim that
Rafter removes npm's script-shell semantics or automatically fixes an installed npm version's escaping rules.

Common typed execution/settings methods may share one composition base so .NET, Git, and npm all delegate to
`ProcessCommand`. Extend RAF003 to these execution surfaces. Add no runtime dependencies, packages, solution/test
projects, or implicit installs. Verify fresh package/file-app/project consumption and real disposable Git/npm fixtures;
keep process/stream settlement, failure/capture/timeout behavior, and caller-owned working directories unchanged.

Local implementation and verification are complete for review: **866 passed, 0 failed, 1 console-dependent skip**
across 867 cases (unit 696, integration 49, Windows 122). Actual Git/frontend file-app examples and fresh tool/package
consumers passed. The Release solution and runtime consumer builds have no warnings/errors. See the
[tool guide and retained failure history](../guides/git-and-npm.md#local-verification-2026-10-27). The original natural
preview launch and controlled mixed-SDK failure remain distinct observations. Inside/outside policy builds and all
**43 redirected runtime cases** also passed. PR #65 subsequently passed Linux portable, Windows portable, and Windows
native evaluation and merged. The post-merge main run also passed. Linux-native execution,
attached-console, and live-editor verification are not established by those checks.

### Conditional targets — agreed behavior, 2026-10-28

**Status: syntax and behavior agreed, implementation authorized on 2026-10-28.** The earlier draft below was
approved by the user. [Issue #66](https://github.com/sotsera/rafter/issues/66) was created before shipping-code changes.
`Target.When(string description, Func<RafterContext, bool> predicate)` is implemented and verified locally.
The runnable [conditions.cs](../../examples/conditions.cs) uses the agreed workflow. Local review on 2026-10-28
found no blocking code issues; applicable PR/main CI and user review remain delivery checkpoints.

The concrete scenario is a Windows repository workflow: always build, optionally skip tests, and pack only when
requested. Existing callbacks can express these branches with `if`, but the target still executes and is counted as
completed. The agreed condition gives the framework an explicit skipped outcome and a description it can display.

Agreed declaration, using the user's preferred multiline block form:

```csharp
var test = command.Target("test", "Run the ordinary repository tests.")
    .DependsOn(build)
    .When("Tests were not disabled with --skip-tests.", context =>
    {
        return !context.GetValue(skipTests);
    })
    .Run(context =>
    {
        context.WriteLine("Run the existing test body here.");
    });
```

`When(description, predicate)` reads as a condition for running this target's body. The description states the
condition; it is not an exception message or a promise that the predicate is true. Existing `.DependsOn(...)` and
`.Run(...)` keep their meanings. Expression lambdas remain valid for simple predicates:

```csharp
.When("Packaging was requested with --pack.", context => context.GetValue(createPackages))
```

A block lambda must explicitly `return` its Boolean result. The implementation keeps both forms through the same
delegate overload; there is no separate block-lambda API. The full example is:

```csharp
var command = Rafter.Command("Build, optionally test, and optionally pack this repository.");
var root = command.Option<string>("root", "Repository root.").Default(".");
var skipTests = command.Option<bool>("skip-tests", "Skip the test target.").Default(false);
var createPackages = command.Option<bool>("pack", "Create local development packages.").Default(false);
command.RootDirectory(root);

var build = command.Target("build", "Build the repository in Release configuration.")
    .Run(async context =>
    {
        await context.Dotnet()
            .UseWorkingDirectorySdk()
            .Timeout(TimeSpan.FromMinutes(5))
            .Build("Rafter.slnx")
            .Configuration("Release")
            .DisableBuildServers()
            .MaxCpuCount(1)
            .RunAsync();
    });

var test = command.Target("test", "Run the ordinary repository tests.")
    .DependsOn(build)
    .When("Tests were not disabled with --skip-tests.", context =>
    {
        return !context.GetValue(skipTests);
    })
    .Run(async context =>
    {
        await context.Dotnet()
            .UseWorkingDirectorySdk()
            .Timeout(TimeSpan.FromMinutes(5))
            .Arguments("test", "--solution", "Rafter.slnx")
            .Arguments("--configuration", "Release", "--no-build")
            .Arguments("--minimum-expected-tests", "1", "--max-parallel-test-modules", "1")
            .RunAsync();
    });

var pack = command.Target("pack", "Create local development packages.")
    .DependsOn(test)
    .When("Packaging was requested with --pack.", context =>
    {
        return context.GetValue(createPackages);
    })
    .Run(async context =>
    {
        var packages = context.Directory("artifacts", "packages").EnsureExists();
        await context.Dotnet()
            .UseWorkingDirectorySdk()
            .Timeout(TimeSpan.FromMinutes(5))
            .Pack("src/Sotsera.Rafter/Sotsera.Rafter.csproj")
            .Configuration("Release")
            .NoBuild()
            .Output(packages.FullPath)
            .RunAsync();
    });

var verify = command.Target("verify", "Build, test when enabled, and pack when requested.")
    .DependsOn(test, pack);

return await command.RunAsync(args, verify);
```

Each body creates its own body-owned dotnet configuration. The test arguments follow this repository's native
Microsoft.Testing.Platform setup and ordinary profile; this is not a universal typed `Dotnet.Test()` proposal.
The root selects this checkout's solution and project paths. The runnable example has explicit per-child deadlines
and disables build servers for the build stage. Its real consumer checks use a miniature disposable solution to
avoid recursively running the outer workflow tests. The four combinations and prerequisite failure all passed.

#### Agreed execution semantics

1. Prepare and validate inputs and the complete static dependency graph using the existing rules. Conditions do not
   hide invalid graphs, declaration errors, or invalid inputs. Help describes conditions without invoking predicates.
2. Visit dependencies first. Evaluate a reachable target's synchronous predicate once, immediately before its body
   would start, with validated inputs and after prior work has settled. Unreachable predicates do not run. Declaration
   order of `.When(...)` and `.DependsOn(...)` does not change this timing.
3. A false result skips only that body. Its prerequisites have already run, and its dependants may continue. Shared
   targets are visited once, so a shared skipped target is not evaluated again. A new invocation evaluates it afresh.
4. A skip is neither a completed body nor a failure. With all executed work successful, it leaves the invocation
   successful. This preserves optional stages: `--pack --skip-tests` deliberately packs after skipping tests. A
   dependency expresses ordering, not a guarantee that an artifact exists or that tests ran; required-artifact and
   required-success policies are separate future decisions.
5. An exception in a predicate is a failure attributed to that target, not a skip. Stop later scheduling. Invocation
   cancellation also stops scheduling using the existing cancellation/failure precedence; it is not a false condition.
   No later predicate is reached after an earlier unhandled dependency failure or cancellation.
6. Start with one described synchronous predicate on a body target. Combine checks explicitly with ordinary C#
   `&&`/`||`. Report declaration errors for null predicates, blank descriptions, repeated `.When(...)`, and conditions
   on dependency-only groups, consistent with existing declaration validation. Async predicates, group/branch pruning,
   multiple condition policies, parallel targets, and cleanup hooks remain outside this increment.

Predicates should inspect prepared inputs or already available state. They are not another asynchronous work body
or a place to start child processes. Ordinary bodies retain existing ownership and settlement behavior.

Assuming successful executed work, the agreed outcome matrix is:

| Inputs | Build | Test | Pack | Invocation summary |
| --- | --- | --- | --- | --- |
| Defaults | Run | Run | Skip | Success; 2 completed, 1 skipped body. |
| `--skip-tests` | Run | Skip | Skip | Success; 1 completed, 2 skipped bodies. |
| `--pack` | Run | Run | Run | Success; 3 completed, 0 skipped bodies. |
| `--pack --skip-tests` | Run | Skip | Run | Success; 2 completed, 1 skipped body. |

Lifecycle output uses stderr and shows a line such as
`[test] Skipped: condition not met (Tests were not disabled with --skip-tests.)`.
Do not emit a body-started/completed pair or a fabricated body duration for a skip. Track completed and skipped body
counts separately; dependency-only groups retain their existing no-body-count behavior. Quiet hides routine skip
lines and summaries, while help shows the condition description without claiming a runtime outcome. Existing
successful summaries without skips retain their wording; summaries with skips append a separate skipped-body count.

The user agreed `.When(description, predicate)`, dependency-first evaluation, body-only guards, and explicit test
skipping permitting packing. Issue #66 verifies the outcome matrix, help/nonexecution, shared dependencies,
declaration validation, predicate failure/cancellation (including cancellation requested during a false predicate),
invocation isolation, and real file-app/project/package consumers. Predicates receive invocation context outside a
body, so they cannot launch body-owned child processes. Caught output failures also remain failures, not skips.

#### Local verification, 2026-10-28

Release solution and runtime-consumer builds passed with **0 warnings and 0 errors**. The full Windows regression
passed **899 tests, 0 failures, 1 existing console-dependent skip** across **900 total**: unit 726, integration 51,
Windows 123 (122 passed, 1 skipped). All **43 redirected runtime cases** passed.

Thirty new unit cases cover the agreed matrix, shared dependencies/order, invalid/unreachable declarations, help,
invalid inputs/graphs, predicate failures/cancellation, snapshots, overlapping invocations, literal/quiet skip output,
and body-only process access. Two real file-app/project consumers each exercise the four combinations, help, and
quiet; existing isolated package consumers also execute/skip packaged conditions. The actual `conditions.cs`
consumer builds/tests/packs a disposable miniature repository for all four combinations and verifies that a failed
build prevents later condition evaluation and packing. It avoids recursively running the outer suites.

Evidence is retained in `artifacts/evidence/conditional-targets/`; final full TRX reports are under
`artifacts/test-results/conditional-full/`. The first unit run's presentation-assertion errors and mistaken minimum
count remain recorded; corrected assertions account for wrapped help and distinguish invocation summaries from
body statuses. No runtime assertion/deadline/timeout was weakened. No CI workflows, project wiring, filters,
dependencies, shared consumer supervisor, or native process ownership implementation changed. New Linux CI,
attached-console/live-editor, macOS, and AOT verification were not run for this increment.

Cleanup/shared resources remain a subsequent example: establish resource acquisition and lifetime, including cleanup
failure, before selecting an API. This increment does not add an always-run target or use skipped work as cleanup.

## Design approach and references

We will shape the new Rafter through small, realistic example scripts, discuss their syntax and behavior, and then
implement connected groups of examples. The aim is to make scripts pleasant to write while checking design assumptions
against working code early.

The original scripts live in [reference/examples](../../reference/examples/). They provide scenarios to consider; their
APIs and feature set are not requirements for the new implementation. Keep those copies unchanged so comparisons remain
possible. Current syntax drafts live in [examples](../../examples/).

See [First working examples](../history/first-working-examples.md) for the historical consistency review, original
implementation scope, and issue outlines, and the [completion summary](../history/milestone-review.md) for delivery.
Use the current position and next steps above for new work; historical recommendations are not pending commitments.
See [Output and help](output-design.md) for the current agreed visual direction and remaining presentation decisions.
Use the [dependency review](../history/dependency-decisions.md) when choosing the SDK, runtime packages, analyzer tooling, and
test dependencies before implementation. Its shortlist remains proposed until selection and compatibility checks.

## Working approach

1. Choose a topic and a concrete script scenario.
2. Draft the intended consumer code before implementing the supporting library. Placeholder work is acceptable when
   the example still makes its purpose and intended behavior clear.
3. Discuss syntax together with observable behavior: what executes, which values are available, how failures stop work,
   and what the user sees.
4. Record agreed decisions and open questions here. Consider alternatives when they resolve a concrete difficulty.
5. Implement a small group of connected examples, then use the working scripts to review the design.
6. Verify the agreed behavior and update the topic's progress with links to the relevant examples and evidence.

We do not need to design every topic before implementing the first group, or finish every edge case before exploring
the next topic. Agreements can be revised when implementation or actual use reveals a better approach.

## GitHub issue workflow

The [repository workflow](../development/github-workflow.md) now records verified identities, PR/merge rules, current enforcement,
and the completed [First working examples milestone](https://github.com/sotsera/rafter/milestone/1). Its evaluation
and runtime issues are closed; the [completion summary](../history/milestone-review.md) records the selected Windows baseline.
Later product topics still need their own discussion and scoped issues.

Keep the initial syntax discussions, decisions, and open questions in Markdown. Introduce GitHub issues after agreeing
on the first group of examples and before implementation begins. Do not create one issue per topic in advance;
topics may merge, split, or be deferred during design.

- Use this plan for design direction and topic progress.
- Use issues for scoped implementation work, bugs, and investigations, with links to the relevant design notes.
- Use focused pull requests to deliver changes and link them to their issues.
- Each issue should describe the intended behavior or problem, scope, relevant examples, acceptance criteria,
  and appropriate verification. Record exclusions where they clarify the scope.
- Close an issue when its scoped work is complete. A broader topic may remain partially complete in this plan.
  Avoid duplicating detailed acceptance checklists between issues and this document.

The **First working examples** milestone is complete. For the next agreed group, create a new scoped milestone only
when its scope is settled. Verify the target repository and acting identity before creating GitHub resources.

## Progress tracking

Track these milestones independently:

- **Drafted:** a proposed example shows the intended authoring experience.
- **Agreed:** the syntax and observable behavior for the selected scope have been discussed and accepted.
- **Implemented:** the example runs against the new implementation.
- **Verified:** appropriate checks demonstrate the agreed behavior, with evidence linked from the topic notes.

In the table, `—` means pending and `✓` means complete. A topic can instead be marked **Deferred** or **Excluded**, with
the reason recorded in its notes. The initial inventory does not commit us to implementing every topic. Split a topic
into smaller scopes when needed; agreement on basic inputs, for example, does not complete all input types or policies.

| Topic | Drafted | Agreed | Implemented | Verified |
| --- | --- | --- | --- | --- |
| Minimal script | ✓ | ✓ | ✓ | ✓ |
| Roots and paths | Partial | Partial | Partial | Partial |
| Filesystem authoring | ✓ | ✓ (first increment) | Merged #51/#53 | Windows full suite, package consumers, and workflow |
| Typed inputs | ✓ | Partial | Partial | Partial |
| Input validation | ✓ | ✓ | ✓ | ✓ |
| Environment inputs | ✓ | ✓ | ✓ | ✓ |
| Targets and dependencies | ✓ | ✓ | ✓ | ✓ |
| Conditional execution | [Build/test/optional-pack example](#conditional-targets--agreed-behavior-2026-10-28) | Agreed `.When(...)` body guard, block/expression lambdas, and skip semantics. | Implemented and locally reviewed under #66; prepared for PR delivery. | Full local 899 passed/0 failed/1 console skip; real consumers and 43 redirected cases passed. PR/main checks pending. |
| Output and diagnostics | Partial | Partial | Partial | Partial |
| Failures | Partial | Partial | Partial | Partial |
| External processes | ✓ | Partial | Partial | Partial |
| Reusable process settings | [Executable and chunked arguments](#process-authoring-draft-for-review) | Body-local immutable defaults, independent commands, and child overrides. | Merged #61, closing #60. | Local and applicable PR/main checks passed. |
| Typed command helpers | [Typed .NET direction](#typed-command-helpers--agreed-direction-2026-10-27) | .NET build/publish/pack and first Git/npm helpers implemented. | Merged through #61/#65, closing #60/#63/#64. | [Local regression and consumers passed](../guides/git-and-npm.md#local-verification-2026-10-27); all #65 PR/main checks passed. |
| SDK environment isolation | [Selected #62 policy](#sdk-isolation-and-gitnpm-helpers--selected-increment-2026-10-27) | Explicit child-directory SDK selection without changing generic inheritance. | Opt-in policy and workflow migration merged #65, closing #62. | Cross-SDK reproduction/fix and workflow checks passed locally; all PR/main checks passed. |
| Captured output | ✓ | ✓ | ✓ | ✓ |
| Cancellation and timeouts | Partial | Partial | Partial | Partial |
| Cleanup and shared resources | — | — | — | — |
| Sensitive values | — | — | — | — |
| Complete workflow | ✓ | Existing API; agreed scenario | Merged #53, closing #52 | Windows consumers and actual repository; see guide |

Failure handling is partially delivered through exception containment, process failure actions, timeout/capture
exceptions, exit codes, and stopping later targets. Broader expected-failure APIs and combined cleanup failures remain
open. Completed cells describe delivered feature scope; the fix backlog above tracks defects within that scope.

## Topic inventory

Several examples cover more than one topic. The links below account for all 23 reference scripts.

| Topic | Questions to discuss | Reference examples |
| --- | --- | --- |
| Minimal script | How much must a greeting declare? Command creation, descriptions, callbacks, invocation, and exit results. | [minimal.cs](../../reference/examples/minimal.cs) |
| Roots and paths | What is the default root? How should invocation, source, and explicit directories affect paths? | [root-invocation.cs](../../reference/examples/root-invocation.cs), [root-source.cs](../../reference/examples/root-source.cs), [root-explicit.cs](../../reference/examples/root-explicit.cs) |
| Typed inputs | Required, optional, and defaulted values; aliases; flags; strings, integers, enums, and durations; value access and nullability. | [options.cs](../../reference/examples/options.cs), [option-types.cs](../../reference/examples/option-types.cs), [durations.cs](../../reference/examples/durations.cs) |
| Input validation | Individual checks, relationships between inputs, validation timing, and how multiple rejections are reported. | [validators.cs](../../reference/examples/validators.cs), [input-rules.cs](../../reference/examples/input-rules.cs) |
| Environment inputs | Explicit mappings, precedence, empty values, conversion, and help behavior. | [environment.cs](../../reference/examples/environment.cs) |
| Targets and dependencies | Entry selection, execution order, shared dependencies, groups, and invalid graphs. | [dependencies.cs](../../reference/examples/dependencies.cs) |
| Conditional execution | When is a condition evaluated? Does skipping affect a body, its dependencies, or an entire branch? | [conditions.cs](../../reference/examples/conditions.cs), [rafter.cs](../../reference/examples/rafter.cs) |
| Output and diagnostics | Results, information, warnings, help, stream routing, and plain, quiet, or verbose output. | [diagnostics.cs](../../reference/examples/diagnostics.cs), [minimal.cs](../../reference/examples/minimal.cs) |
| Failures | Expected failures, unexpected exceptions, process failures, exit codes, and stopping subsequent callback statements. | [diagnostics.cs](../../reference/examples/diagnostics.cs), [processes.cs](../../reference/examples/processes.cs) |
| External processes | Executables, argument tokens, working directories, child environments, streaming, and process results. | [processes.cs](../../reference/examples/processes.cs) |
| Reusable process settings | Sharing defaults, applying overrides, and resolving input-dependent settings. | [process-configuration.cs](../../reference/examples/process-configuration.cs) |
| Captured output | Reading structured results, optional live echo, capture limits, overflow, and availability of captured data. | [process-capture.cs](../../reference/examples/process-capture.cs) |
| Cancellation and timeouts | Cooperative managed work, child shutdown, grace periods, independent deadlines, and actual completion. | [process-cancellation.cs](../../reference/examples/process-cancellation.cs), [process-timeout.cs](../../reference/examples/process-timeout.cs), [managed-timeout.cs](../../reference/examples/managed-timeout.cs), [cleanup-timeout.cs](../../reference/examples/cleanup-timeout.cs) |
| Cleanup and shared resources | Target and command lifetimes, state shared between steps, cleanup eligibility, and combined failures. | [cleanup.cs](../../reference/examples/cleanup.cs), [cleanup-timeout.cs](../../reference/examples/cleanup-timeout.cs) |
| Sensitive values | Declaring sensitive inputs, redacting presentation, raw value access, and the boundaries of protection. | [redaction.cs](../../reference/examples/redaction.cs) |
| Complete workflow | Does the combined API remain readable in a build, test, and optional packaging script? | [rafter.cs](../../reference/examples/rafter.cs) |

## First group of examples

The original delivery sequence was:

1. **Minimal script:** perform one small action and return an outcome.
2. **Basic inputs:** add a required value, a defaulted value, and a Boolean flag.
3. **One external process:** invoke a tool with explicit arguments and handle its outcome.
4. **Two dependent targets:** connect real steps and establish what happens when the first step fails.

This foundation was delivered in the completed first milestone. It remains here as design history; use
[Next steps](#next-steps) for the current proposed sequence.

## Questions raised by the reference examples

These observations are starting points for discussion, not decisions about the new API.

- **Initial ceremony:** the greeting requires a root policy, command description, target name, target description,
  callback, and explicit entry selection. Which should be required, optional, or supplied by a default?
- **Input access:** declarations express useful guarantees, but larger scripts repeat `context.Value(...)` frequently.
  Can reading values be simpler while preserving validation timing and type guarantees?
- **Stopping after failure:** process consumers repeat `if (!result.Succeeded) { return; }`, and `context.Fail(...)`
  records a failure without stopping subsequent statements. What should the normal failure path look like?
- **Conditional branches:** dependencies run before a condition, and dependents can continue after a skip. The packaging
  workflow repeats one condition on two targets. What should a condition govern?
- **Shared state and cleanup:** the cleanup example captures nullable variables and uses `sharedFile!`. How should
  resources and values pass between steps, and when should their lifetime end?
- **Implementation details in scripts:** examples repeat a timeout bound based on `uint.MaxValue` and use
  `result.Capture!` after requesting capture. Which constraints and guarantees should the library express directly?

## Decisions and discussion notes

Established foundations:

- Keep Rafter, Sotsera branding, and `Sotsera.Rafter` naming.
- Make script output visually clear and pleasant for humans while keeping descriptions, help, and diagnostics useful
  to agents. The initial human/plain presentation is recorded in [Output and help](output-design.md); broader output
  details and any machine-readable interface remain open design topics.
- Start with a fresh implementation and retain the old examples as reference material.
- Use Markdown for project documents.
- Explore examples and behavior before implementing small connected groups, with separate design and delivery progress.

As topics are discussed, add a subsection for each with its scenario, proposed example link, agreed syntax and behavior,
open questions, and implementation or verification evidence. Record deferrals and exclusions there too. Keep the
progress table aligned with those notes, and revise superseded decisions so the current direction remains clear.

### Output and diagnostics

The [output design](output-design.md) records the direction accepted for now: compact target sections, consistent
indentation, visible descriptions, own-work target timings, one final invocation result, terminal-only result/child
prefixes, clean redirected results, grouped input errors, and separate command/execution/Rafter help sections.
The selected flatter layout puts target descriptions at column zero and uses one two-space content/diagnostic gutter,
preserving original content indentation. Cancellation-request messages are yellow in styled output and hidden in quiet mode.
Results and help go to stdout; lifecycle and diagnostics go to stderr; child streams retain their destinations.
Help lists primary option names first, then aliases in declaration order, separated by commas. Capable stdout terminals
style help headings and names, with equal emphasis for primary names and aliases, muted separators, and distinct,
readable type hints. The selected preview palette uses lavender type hints alongside blue option names.
Plain or redirected help preserves the text without Rafter styling.
Normal target headings include direct dependencies in declaration order: `[test] <-- (build)`, with the suffix
muted in styled output and retained in plain text. Entry groups use the same notation, such as
`> verify <-- (build, test)`. Omit the suffix when there are no dependencies; quiet hides these routine headings.
Body-only scripts do not acquire an implicit target. Normal, `--quiet`, `--verbose`, and independent `--plain` controls
are agreed as described in the output design. Quiet and verbose are mutually exclusive; redirected streams automatically
use plain presentation. Help organization for options, individual/combined validation rules, and the selected dependency
workflow is accepted through the three preview help examples. Multiple-entry headers and final summaries preserve
the supplied entry order; target sections follow dependency execution order, counting each executed body once and
excluding dependency-only groups from that count. Do not introduce a synthetic group name or imply that one entry
owns all selected work. In the first version, process output identifies the executable; verbose output and relevant
failure diagnostics include the child working directory. Rafter does not automatically print arguments or environment
values, even in verbose mode. Explicit selected-argument disclosure with redaction is deferred; script/child output
and exception details retain their existing policies. Further verbose content and remaining failure transcripts are still open. The old
[output previews](../../reference/output/README.md) are preserved unchanged for comparison.

Failure detail is agreed: invalid declarations/inputs use grouped diagnostics without stacks; rejected process exits
identify the applicable target, executable, actual code, and accepted codes without framework stacks. Unexpected callback
exceptions retain scope, type, message, inner exceptions, and full stack traces in every verbosity mode, including quiet
and plain/redirected output. Final execution summaries identify the outcome and stopping point without repeating details.
Process diagnostics use scenario 18's compact status/executable heading and aligned details. Handled `Continue`
failures use yellow; unhandled or recorded invocation failures use red. Compact callback-failure headings retain
full exception details, and plain output retains all wording without color. This presentation does not change failure actions.

The topic is partially drafted and agreed. The new [HTML comparison](output-preview.html) contains 23 illustrative
scenarios with terminal/plain, stream, verbosity, and width controls. Remaining diagnostic wording
remains proposed for review. The [minimal runtime](../guides/minimal-runtime.md) now verifies body-only output and help;
[target output](../guides/targets.md) and [Windows process output](../guides/process-runtime.md) are implemented too. Historical previews and synthetic
fixtures are not runtime evidence.

### Minimal script

Scenario: print a greeting using a single command. [examples/minimal.cs](../../examples/minimal.cs) is now runnable
against the new runtime through a project reference. See [implementation and verification](../guides/minimal-runtime.md).

Agreed:

- Commands declare metadata and inputs. Select execution at invocation time: `RunAsync(args, body)` runs a supplied
  synchronous or asynchronous callback; `RunAsync(args, target, additionalTargets)` runs selected targets and dependencies,
  with one required target and a `params` array of additional targets.
- Commands have no `.Run(...)` method or stored body. Target declarations retain `.Run(...)` to register their bodies.
- The command description is mandatory and must contain non-whitespace text. Non-null alone is insufficient.
  Descriptions should explain the script's purpose to both humans and agents.
- Supply the required description in `Rafter.Command(description)` rather than a separate fluent `.Description(...)`
  call. Omitting the argument is a compiler error in the intended API; long descriptions can wrap across lines.
- `RunAsync` validates the final description during initial validation, before executing the command body, even when
  analyzers are available. Null, empty, and whitespace-only descriptions produce a concise invalid-command diagnostic
  and exit code `2`. Command declaration records the description without throwing a validation exception.
- Use `context.WriteLine(...)` for script results, without an intermediate `Output` member. The broader rendering
  and diagnostic APIs remain open.
- Successful execution and requested help return `0`; invalid declarations or arguments return `2`; execution
  failures return `1`. Help does not execute the command body. See the invocation behavior below.
- `RunAsync` contains recoverable exceptions from invocation setup, validation, execution, and reporting, including
  awaited user callbacks, and returns an exit code rather than propagating those exceptions to the caller.
- `Rafter.Command(description)` requires no explicit root. It captures the process working directory when `RunAsync` starts,
  uses that root for Rafter-resolved relative paths, and does not change the process working directory.
  See [roots and paths](../guides/roots-and-paths.md) for the agreed behavior and examples.
- Body execution and target execution are separate invocation forms. Target execution does not append an additional
  command body. Both forms share validation, help, root capture, exception containment, and exit-code rules.

Open questions:

- What analyzer scope, severity, and packaging should enforce nonblank descriptions during authoring?
- Timeout policies and broader process cancellation remain in their dedicated topic. Cooperative managed cancellation
  is implemented for the body-only invocation.

The body-only scope and [target invocation form](../guides/targets.md) are implemented and verified. Initial
[analyzer authoring feedback](../guides/authoring-diagnostics.md) is included in the local package.

#### Description validation

Initial runtime validation in `RunAsync` is agreed. It checks the final description before executing the body and
reports invalid descriptions through a concise diagnostic and exit code `2`, rather than an unhandled exception.
This remains necessary for dynamic values, disabled analyzers, and consumers that bypass static checks.

Initial authoring diagnostic:

RAF001 reports an error for description arguments whose compiler constant value is null, empty, or whitespace.
It includes constant expressions rather than checking only string literals. Dynamic expressions are left to runtime
validation rather than rejected merely because their values cannot be determined statically.

Requiring the description argument at command creation handles omission through the API itself, without analyzing
fluent call sequences. The initial implementation uses the proposed error severity across commands, options, targets,
and validation rules. See [diagnostic coverage and limits](../guides/authoring-diagnostics.md#raf001).

#### Invocation behavior

The agreed API has two execution forms, with separate delegate overloads for synchronous and asynchronous bodies:

```csharp
Task<int> RunAsync(string[] args, Action<RafterContext> body);
Task<int> RunAsync(string[] args, Func<RafterContext, Task> body);
Task<int> RunAsync(string[] args, Target target, params Target[] additionalTargets);
```

These are the agreed public signature shapes. The two body overloads and the required-entry target overload are implemented.
For a body invocation, validate and then execute the supplied callback. For a target invocation, validate and then
execute the selected dependency graph. Validation and help execute neither form's work callbacks.

| Outcome | Exit code | Behavior |
| --- | --- | --- |
| Successful execution | `0` | The supplied body or selected target workflow completes successfully. |
| Requested help | `0` | Display help without executing the supplied body or target callbacks. |
| Invalid declaration or arguments | `2` | Report validation diagnostics without executing work callbacks. |
| Execution failure | `1` | Report the failure; unexpected callback exceptions include their details. |
| Invocation cancelled | `130` | Report cancellation after running callbacks and owned process/output work settle, provided no unhandled or recorded execution failure remains. |

`RunAsync` is the exception boundary for the invocation. Callers should not need a surrounding `try/catch` to handle
ordinary command failures. Recoverable exceptions from Rafter operations and synchronous or awaited asynchronous
callbacks are caught and translated into failure outcomes. Exceptions during setup, validation, or reporting are
also contained; a thrown validator or framework exception is an execution failure, distinct from an ordinary
validation rejection. Failures must remain visible in diagnostics rather than being silently swallowed.

If reporting itself fails, return a failure result and attempt reporting only where possible; do not allow a second
reporting exception to escape. Output cannot be guaranteed when the destination is unavailable.

The boundary covers work executed or awaited by the invocation. It cannot contain exceptions raised while the caller
evaluates arguments before entering `RunAsync`, detached background work, or process-ending events such as a fatal
runtime failure or forced termination. No exit result can be promised when the process cannot continue.

Cancellation uses exit `130` when cleanup succeeds and no unhandled or recorded execution failure remains. Execution
failures and unconfirmed process/output settlement take precedence with exit `1`, as described below. Timeout policy
remains open. Managed body cancellation and Windows owned-process cancellation are implemented; see the
[process runtime](../guides/process-runtime.md) for verification and platform limits.

#### Help and validation

Agreed behavior:

1. Validate command declarations first, including the required nonblank description. Invalid declarations return `2`
   even when `--help` is supplied.
2. For valid declarations, parse the supplied arguments. Explicitly supplied invalid arguments still return `2` when
   combined with `--help`; help does not silently ignore malformed or unrecognized arguments.
3. If help is requested and those checks pass, display help and return `0` without executing the supplied body or targets.
   Omitted required input values do not prevent help, and help does not evaluate user validation callbacks.

For example, assuming a declared required integer option named `count`, `--help` succeeds without a value, while
`--help --count nonsense` returns `2` because the supplied value cannot be parsed as an integer. This example describes
help behavior independently of how the body accesses input values.

### Roots and paths

The default root behavior is agreed and documented in [roots and paths](../guides/roots-and-paths.md): capture the invocation's
working directory at `RunAsync`, retain it for that run, and resolve Rafter-managed relative paths against it without
changing the process working directory. Ordinary .NET file operations are unaffected by a Rafter root selection.

Child processes use the command root by default. `.WorkingDirectory(path)` overrides the child's working directory,
resolving relative paths against the command root and using absolute paths directly, without changing the command
root or the script process's working directory. See the external-process notes and root guide for examples.

The [2026-10-25 root/path design](roots-and-filesystem.md) adds agreed public string `RootDirectory`,
`ResolvePath(path)`, and literal/prepared-option root declarations. This first increment merged through PR #50
under [issue #46](https://github.com/sotsera/rafter/issues/46); the current guide records its behavior and the design
records verification. The later [filesystem increment](filesystem-authoring.md) is also agreed and implemented
through PR #51 under #47. Source/repository discovery and broader filesystem operations remain separate work.

### Invalid values and repeated callback configuration

Agreed declaration rules:

- `.Default(null)` is invalid: a defaulted input promises a non-null value. An optional input without a default
  represents absence. Non-nullable annotations do not replace runtime validation of supplied defaults.
- Null callbacks are invalid, including command/target bodies, validation predicates, and process failure handlers.
- Calling `.Required()` more than once or `.Default(...)` more than once on an option is invalid, even when the
  repeated default is identical. Mixing those policies is also invalid. Initial validation catches separate calls
  through a retained original handle; [RAF004](../guides/authoring-diagnostics.md#raf004) reports supported straight-line repetitions.
- Calling `.Run(...)` twice on the same target is invalid; do not silently replace or combine target bodies.
- Calling `.OnFailure(...)` twice on the same process configuration is invalid; do not silently replace handlers.
- Collection-style methods such as `.Aliases(...)` and `.Validate(...)` continue to append according to their own
  rules. Repeated configuration is not universally invalid.

For command, input, and target declarations, report these errors during initial command `RunAsync` validation with
exit `2`, before executing user work. Fluent declarations collect configuration without throwing validation errors.
This includes supplied null command bodies even though the body is passed directly to `RunAsync`.

Process configuration created inside an executing body cannot be checked by the command's initial validation. Validate
it when that process's `RunAsync` begins, before attempting to start the child. Invalid process configuration throws
inside the body; if unhandled, the outer command reports execution failure with exit `1`, as specified below. These
errors are not initial command declaration failures with exit `2`.

### Typed inputs

Scenario: print a personalized greeting with a required name, defaulted repeat count, and defaulted uppercase flag.
The runnable example is [examples/options.cs](../../examples/options.cs), including declarations and the greeting body.
The [typed-input runtime record](../guides/typed-inputs.md) describes the string/int/bool implementation and verification.
Additional types remain pending; the initial nullable-type warning is described in [authoring diagnostics](../guides/authoring-diagnostics.md).

Agreed declaration syntax and behavior:

- Declare an input with `command.Option<T>(name, description)`. Its description is required, must contain non-whitespace
  text, and is checked during initial validation, like the command description.
- `.Required()` requires a supplied value; `.Default(value)` provides a fallback when the input is omitted.
- Required means present, not nonempty, positive, or true. An explicitly supplied empty string, zero, or false
  satisfies the presence requirement. Parsing still checks the declared type, and explicit validators decide whether
  a supplied value is acceptable. For example, rejecting a blank name or a negative repeat count requires a validator.
- With neither, the input is optional and omission is distinct from a supplied value such as zero or false.
- Use the non-nullable supplied type as `T`. The required, defaulted, or optional declaration determines the
  nullability of the value exposed to the script body.
- Read an option's validated value for the current invocation with `context.GetValue(option)`. The option handle
  represents the declaration; value lookup is explicit through the invocation context.
- `GetValue` reads the value resolved and validated during the initial phases of that `RunAsync` invocation, before
  the supplied body or selected target work begins. It does not reparse arguments, reevaluate defaults or validators,
  perform I/O, or recompute the value on each call. Stored values belong to the invocation, not to the option declaration.
- Required/defaulted handles expose non-nullable values; optional handles expose nullable values. For example,
  required/defaulted string and integer inputs return `string` and `int`, while optional inputs return `string?`
  and `int?`. `Option<T>` is the optional declaration handle; `ValueOption<T>` is the finalized required/defaulted
  handle. See the [lookup signatures](../guides/typed-inputs.md#typed-handles-and-lookups) for the implemented initial types.

#### Enum inputs

Initial scope agreed on 2026-10-22: accept enum member names case-insensitively in command-line/environment input,
reject numeric text, and defer `[Flags]` support. Preserve the existing option policies, typed lookup, source precedence,
and validation rules. [examples/enums.cs](../../examples/enums.cs) is implemented; the [current guide](../guides/enum-inputs.md)
records behavior and verification.
The [enum-input design](enum-inputs.md) records these decisions and the agreed allowed-values/default help layout.
Declaration validation rejects case-ambiguous names, shared numeric values, flags, and undefined defaults before help.
Supplied values trim surrounding whitespace; invalid or empty values produce source-aware errors without echoing them.
[Issue #38](https://github.com/sotsera/rafter/issues/38) tracks implementation and consumer verification.
The broader typed-input topic stays partial; additional input types and policies remain outside the delivered slices.

#### Duration inputs

Initial scope agreed on 2026-10-22: use `Option<TimeSpan>` with the existing presence policies, environment fallbacks,
typed lookup, and prepared-value behavior. Start with explicit unit forms such as `250ms`, `30s`, `5m`, and `1h`;
reject bare numbers. Durations may be signed; a nonnegative restriction belongs in an option validator.
Compound forms such as `1h30m` are accepted: descending units without repetition, one sign for the whole duration,
and a fraction only on the final component. Nonnormalized magnitudes such as `1h90m` are valid; clock notation is excluded.
Help uses the agreed `<duration>` placeholder, compact defaults, and a units line before environment metadata and rules.
Defaults normalize into descending components (`90s` becomes `1m30s`), omitting zero components and retaining exact precision.
[examples/durations.cs](../../examples/durations.cs) is implemented and only prints values. The
[duration-input design](duration-inputs.md) records the agreed authoring, help, conversion, and formatting rules;
the [current guide](../guides/duration-inputs.md) records behavior and verification.
[Issue #40](https://github.com/sotsera/rafter/issues/40) tracks delivery. Configurable timeouts remain a separate topic.

#### Names and aliases

Agreed naming rules, with the requested extension for option aliases and explicit prefixes:

- Names use lowercase letters and digits, start with a letter, and allow single hyphens between nonempty segments
  (`output-directory`, `run-tests`). Match names case-sensitively; `--Name` does not match `--name`.
- Require unique target names and unique option names. Targets and options have separate namespaces and may share a name.
- Reserve `help`, `quiet`, `verbose`, and `plain` for Rafter options, including aliases. Those may still be target names.
- For option names and aliases without an explicit prefix, add `-` for one character and `--` for longer names.
  If a prefix is already supplied, preserve it rather than adding another prefix. Apply the name grammar to the part
  after the option prefix. Target names are not option tokens and do not receive these prefixes.
- Check invalid declarations during initial `RunAsync` validation, not while collecting fluent configuration.

Agreed API shape: `.Aliases(params string[] aliases)`. The current syntax draft uses:

```csharp
var name = command.Option<string>("name", "The person to greet.")
    .Aliases("n", "recipient")
    .Required();
```

| Declaration spelling | CLI spelling |
| --- | --- |
| `name` | `--name` |
| `n` | `-n` |
| `--name` | `--name` |
| `-n` | `-n` |
| `-name` | `-name` (explicit prefix preserved) |
| `--n` | `--n` (explicit prefix preserved) |

Aliases identify the same input and resolved value as its primary name. Supplying both `--name Ada` and `-n Ada`
counts as repeating the scalar option and is invalid, even though the values match. Help should show the declared
spellings together. This does not introduce target aliases or short-option bundling.

Agreed alias edge policies:

- Check collisions across primary names and aliases after prefix expansion; reject a spelling owned by two options.
- Append aliases on repeated `.Aliases(...)` calls and ignore duplicate spellings on the same option.
- Reject null/blank aliases, prefix-only tokens, and more than two leading dashes during initial validation.

Alias declaration errors produce exit `2` before execution. For example, `"n"` and `"-n"` expand to the same spelling:
they are harmless duplicates on one option but a conflict across different options. Explicit `"--n"` remains a distinct
spelling. Duplicate aliases do not permit repeated CLI occurrences of the same scalar input.

Handle design requirement:

- Keep aliases available on optional, required, and defaulted handles without changing the handle's value guarantees
  or re-exposing the mutually exclusive Required/Default methods.

#### Boolean syntax and repeated scalar options

Agreed:

| Supplied input | Resolved Boolean value |
| --- | --- |
| `--uppercase` | `true` |
| `--uppercase true` or `--uppercase=true` | `true` |
| `--uppercase false` or `--uppercase=false` | `false` |
| Option omitted | Use the declared default; otherwise apply its required/optional policy. |

Explicit `false` counts as supplied for a required Boolean option. An omitted optional Boolean without a default
remains absent rather than implicitly becoming false. The options example declares `.Default(false)` explicitly.

Reject repeated occurrences of a scalar option as invalid input (exit `2`), even when their values match. For example,
`--repeat 2 --repeat 3` and `--uppercase --uppercase=false` do not silently select the first or last value. This rule
applies before body or target execution. Collection-valued inputs are a separate future design topic.

Boolean literals are case-insensitive: `true`, `True`, and `TRUE` resolve to true, and corresponding false spellings
resolve to false. This is independent of case-sensitive option-name matching.

Additional syntax agreed during the parser evaluation:

- `--name=` supplies an explicit empty string, just like a separate empty argument. Empty Boolean and numeric values
  are invalid; `--flag=` must not become a bare true flag.
- A declared option starts a new input: `--name --help` reports a missing name value. Use `--name=--help` to pass that
  literal text as the name. The same boundary applies to declared aliases and attached option spellings.
- Colon option separators, response-file expansion, and directives are unsupported initially. Colons and equals
  signs inside string values remain literal. An `@file` supplied as a string value does not read that file.

The [parser evaluation](../history/parser-evaluation.md) exercises these rules through direct arrays and a real file app.
That evaluation verifies a fixed adapter; the [runtime tests](../guides/typed-inputs.md#verification) now verify dynamic
declarations too. Positional arguments and a standalone `--` remain unsupported in the initial runtime grammar.

#### Input diagnostics and API constraints

Agreed:

- An analyzer warns for nullable option type arguments, including nullable reference types such as `string?` and
  nullable value types such as `int?` or `Nullable<int>`. Explain that authors should supply `string` or `int` and let
  the option declaration determine omission behavior.
- `.Required()` and `.Default(...)` return a handle that exposes neither method. This prevents contradictory
  fluent chains in either order through the API, without requiring analyzer-based chain tracking.
- Conflicts introduced through separate statements or helper methods are rejected during initial `RunAsync`
  validation with exit code `2` before the body executes. Fluent constraints do not replace runtime validation.
- Repeating either policy through a retained optional handle is also invalid: reject both repeated `.Required()`
  and repeated `.Default(...)`, including identical defaults. Do not silently accept or replace either policy.
- Reject null defaults during initial validation to preserve the promised non-null value of defaulted inputs.

Possible additions and open details:

- A code fix could remove nullable syntax where safe. [RAF004](../guides/authoring-diagnostics.md#raf004) now flags repeated or
  conflicting policies through the same local in supported straight-line statements. Broader flow and alias analysis
  remain deferred; detection across arbitrary helpers is not a static guarantee.
- Further input types remain open. The initial typed handles, runtime policy checks, and nullable-type warning RAF002
  are implemented; nullable reference type annotations cannot be detected at runtime.

#### Future asynchronous value resolution

Deferred idea: an option might asynchronously resolve or transform a user-supplied value at runtime. For example,
an environment name could be resolved to configuration from a service, or an identifier to a domain object.
This is a future design topic, not part of the initial implementation or a committed API.

Preferred direction to explore:

- Keep `context.GetValue(option)` synchronous and free of hidden work. Await resolution during invocation preparation,
  validate the resulting value, and store it before executing the body or targets. Repeated reads use that stored value.
- Do not run asynchronous resolvers for help or during fluent declaration. Define how required/defaulted and optional
  inputs participate, and preserve the distinction between the supplied value and a resolved value if useful.
- Decide input/output types, validation ordering, relationships between resolvers, cancellation, deadlines, and error
  reporting before choosing syntax. Avoid making invocation preparation an implicit workflow engine.
- If a value depends on work performed by a target, explore explicit target results or shared workflow state instead
  of silently changing an option value during execution.

No asynchronous option syntax or implementation is selected. Revisit this note when a concrete script needs it.

### Environment inputs

The [environment-input design](environment-inputs.md) records behavior agreed on 2026-10-22.
[examples/environment.cs](../../examples/environment.cs) now uses the implemented `.FromEnvironment(...)` API.
[Issue #36](https://github.com/sotsera/rafter/issues/36) scopes implementation and verification, described in the
[current guide](../guides/environment-inputs.md). The reference example remains unchanged.

Agreed: command line precedes environment, then default and required/optional behavior. Empty is supplied, not missing:
accept it for strings subject to validation, and reject it for integer/Boolean conversion without falling back. Resolve
once during invocation preparation so `GetValue` keeps reading the prepared value. Help identifies the mapped variable
from declarations without reading/converting environment values or running input validators; existing declaration and
malformed-argument checks still apply. Invalid or empty environment values do not block help. During execution,
verbose source diagnostics identify the selected source and mark empty environment values, while normal/quiet output
retains source context for input errors. Do not automatically print value contents in source diagnostics.

`FromEnvironment` is available before and after `.Required()` or `.Default(...)`, preserving the handle kind and source
precedence. Reject repeated mappings on the same option during initial declaration validation (exit `2`), including
identical mappings and calls through retained/finalized handles. Reject null/blank names, `=`, NUL/control characters,
and leading/trailing whitespace as declaration errors. Preserve names without trimming/case conversion; use native
case matching (Windows case-insensitive, Linux case-sensitive), with uppercase spelling recommended for portability.
Preserve string whitespace exactly. Ignore surrounding whitespace for integer/Boolean conversion; whitespace-only
typed values fail rather than falling back. Booleans accept only case-insensitive `true`/`false`, with no numeric or
yes/no/on/off forms. Do not interpret quotes or escapes in environment text. Help adds mapping metadata before rules
and explains precedence once. After successful validation, verbose stderr lists every option's selected source in one
`Input sources` block before execution, using declaration order and primary names without value contents. The selected
design is implemented; use the linked guide and delivery PR for observed verification results.

### Input validation

Scenario: validate two defaulted range bounds individually and together, with multiple rules per bound and an optional
label whose validation is skipped when omitted. The runnable example is [examples/validation.cs](../../examples/validation.cs).
The individual and cross-input rule design below is implemented for string/int/bool;
see the [runtime record](../guides/typed-inputs.md) for verification and remaining scope.

Agreed individual value validation:

- Register a rule with `.Validate(description, predicate)`. Its description must contain non-whitespace text and
  explain the requirement; initial declaration validation checks it before callbacks execute.
- A predicate returns `true` to accept a value and `false` to reject it. Declaration registers the predicate without
  executing it.
- Validators run during `RunAsync` before the command body and also check default values used for omitted inputs.
- Omitted optional values skip value validation. Explicitly supplied values, including empty strings, zero, and false,
  are checked. The predicate receives the supplied value rather than an optional omission marker.
- Repeated `.Validate(...)` calls append rules. Ordinary rejections are collected and reported together, with exit
  code `2` and no command body execution.
- An exception thrown by a validator stops further validation, is contained, and is reported as an execution failure
  with exit code `1`. Preserve diagnostics already collected before the exception; do not execute the command body.
- Help displays rule descriptions without executing the predicates.

Agreed cross-input validation:

- Register a rule with `command.Validate(description, predicate)`, using a mandatory nonblank description that explains
  the requirement. Initial validation checks the declaration without executing the predicate.
- The predicate receives an input-validation context with `inputs.GetValue(option)` for reading validated input values.
  This context does not expose execution facilities such as process launching.
- Cross-input rules run only after argument parsing and individual value validation succeed. They run before the
  command body and can express relationships, mutually exclusive flags, and inputs that require other inputs.
- Collect ordinary rejections and return `2` without executing the body. If a predicate throws, stop further validation
  and return `1`, preserving earlier diagnostics and reporting the exception.
- Include rule descriptions in help without evaluating predicates.

### External processes

Scenarios: display the installed .NET SDK version, inspect a failed build while retaining command failure, and build
from a child-specific working directory with an environment override.
The runnable Windows examples are [examples/processes.cs](../../examples/processes.cs) and
[examples/process-failure.cs](../../examples/process-failure.cs), plus
[examples/process-working-directory.cs](../../examples/process-working-directory.cs). See the
[process runtime record](../guides/process-runtime.md) for implementation, verification, and remaining platform/settings scope.

Agreed basic syntax:

- `context.Executable(executable)` selects reusable executable configuration. Its first `.Arguments(...)` call creates
  an independent child command; see the [executable design](#process-authoring-draft-for-review) for defaults and reuse.
- `.Arguments(...)` supplies separate argument tokens.
- The process's `RunAsync()` starts and awaits the process, returning a process result whenever control returns normally
  to the body. The author can use or ignore that result. There is no separate result-returning execution method.
- The outer `command.RunAsync(args, body)` or `command.RunAsync(args, targets)` remains the invocation boundary and
  returns the command's exit result.
- The child's working directory defaults to the command root. `.WorkingDirectory(path)` selects a directory for that
  child only: relative paths resolve against the command root; absolute paths are used directly. It does not change
  the command root, script process working directory, or parent shell directory.
- Children inherit the script process's environment by default. `.Environment(name, value)` explicitly sets or
  overrides a variable for that child only, without changing the script process's or parent shell's environment.
- Each environment name may be configured once on a child process builder; repeated calls for that name are rejected,
  even with the same value. Names are case-insensitive on Windows. Overriding an inherited variable is allowed, and
  separate child builders may choose different values for the same name.
- `WorkingDirectory` and `AcceptExitCodes` may each be called once per builder. Repeated calls are rejected.
  Accepted-code lists must be non-null and nonempty; repeated codes within one list are ignored.
- Command options are not automatically exported as environment variables. Authors can explicitly pass an option's
  value to a child environment setting when needed.

Agreed success and result behavior:

- Success means the process starts, finishes without cancellation or timeout, exits with an accepted code, and Rafter
  completes its owned output handling successfully. Process completion alone does not establish success.
- Only exit code `0` is accepted by default. `.AcceptExitCodes(0, 1)` illustrates explicitly choosing accepted codes
  for a tool whose additional codes represent valid outcomes.
- The result exposes at least `Succeeded` and `ExitCode`. A handled rejected exit remains unsuccessful in the result;
  handling determines control flow and command outcome, not whether the process succeeded.

Agreed failure handling:

- `.OnFailure(result => ...)` handles completed processes with rejected exit codes and returns a `ProcessFailureAction`.
  The actions are `Continue`, `Fail`, and `Throw`.
- With no handler, the default action is `Throw`. Ordinary process calls therefore do not need repetitive result checks.

| Outcome or action | Returns a result | Current body | Command outcome |
| --- | --- | --- | --- |
| Accepted exit code | Yes | Continues | Can succeed. |
| `Continue` | Yes | Continues | This process failure is handled and does not by itself fail the command. |
| `Fail` | Yes | Continues until the author returns or the body finishes | Failure is recorded; later targets do not run and the command returns `1`. |
| `Throw` or no handler | No | Unwinds unless script code catches the exception | The outer command catches an unhandled process failure and returns `1`. |

`Fail` does not skip subsequent statements in the current body. The author can inspect the returned result, perform
additional work, and explicitly `return`. `Continue` does not clear failures already recorded elsewhere.

Accepted reporting: once the handler selects `Continue`, emit one yellow handled-failure warning with the executable,
actual and accepted exit codes, and action. Keep that warning in quiet mode; the invocation can still succeed.
Once the handler selects `Fail`, record invocation failure and emit the red process diagnostic before returning the
unsuccessful result to the body. The body may inspect that result and continue with cleanup or other work; later
targets do not start. Keep the diagnostic in quiet mode. The final invocation summary reports failure without
repeating the process diagnostic, and subsequent successful work does not clear the recorded failure.
For `Throw` or its default, defer the framework failure diagnostic until the exception escapes the body. If script code
catches and handles it, preserve original child output and let the script explain its recovery without an automatic
framework error or stack trace. Independent recorded failures and cleanup uncertainty remain reportable.

`Throw` uses a specific process-failure exception carrying the process result. Ordinary C# `finally` blocks still run
as the body unwinds. The outer command reports a concise process failure without propagating it to its caller;
recognized process failures do not need an unexpected-error stack trace as their normal presentation.

If the failure handler throws, treat that as a callback failure and preserve the original process failure too.
Launch errors, cancellation, timeouts, and output failures are outside this exit-code handler's scope; the callback
must not silently turn them into handled unsuccessful exits.

Agreed process exception boundary:

- Invalid process configuration, launch failures, and owned output-handling failures cause process `RunAsync` to throw
  a descriptive exception inside the body. If unhandled, the outer command contains it, reports failure, and returns `1`.
- These failures do not invoke `OnFailure` or fabricate a normally returned completed-process result. Check process
  configuration before attempting to start the child.
- Deliberately catching a process exception handles that failure. The script can recover and ultimately succeed if
  no other failure was recorded. This includes a caught rejected-exit `Throw`; throwing alone does not mark an
  irreversible command failure.
- An already recorded `ProcessFailureAction.Fail` remains a command failure despite subsequent successful work.
  Catching another exception or handling another process failure does not clear that state.
- Recovery does not remove Rafter's resource-ownership obligations. Process/output settlement still applies before
  reporting completion. Invocation cancellation follows the separate policy below; clean timeout recovery follows
  the agreed per-process contract.
  Catching process exceptions does not clear invocation cancellation.

Open questions:

- What additional executable-resolution controls, stream encodings, and other process settings should be exposed?
- Starting with an empty environment remains undesigned; explicit child-only `RemoveEnvironment(...)` is implemented
  locally under #60.

The command-level exception containment contract applies to the outer invocation. Process `RunAsync()` intentionally
throws within the callback for the `Throw` action; these are separate API boundaries. The
[Windows implementation](../guides/process-runtime.md) verifies this boundary with real processes.

#### Per-process timeouts

Initial scope agreed on 2026-10-22: add `.Timeout(TimeSpan)` for one process operation, with no timeout by default and
positive configured durations. Start its deadline when the child begins execution; expiry initiates owned-scope shutdown
and settlement, not an immediate return guarantee. An unhandled timeout fails with exit `1`, distinct from Ctrl+C;
`OnFailure` remains limited to rejected exits. Target, invocation, and cleanup deadlines stay separate.
[examples/process-timeout.cs](../../examples/process-timeout.cs) is implemented for [issue #42](https://github.com/sotsera/rafter/issues/42).
The [timeout design](process-timeouts.md) records agreed scope, catchable recovery, exception details/output, and race
rules and configuration validation. The [implementation guide](../guides/process-timeouts.md) records delivery.
A settled `ProcessTimeoutException` can be caught for recovery without automatically recording
failure; independent failures, invocation cancellation, and settlement uncertainty remain in force.

### Captured output

Implemented scope and [runnable example](../../examples/process-capture.cs), 2026-10-23: capture separate bounded stdout/stderr
text for script inspection, silently by default with optional live echo. The combined execution name
`RunAndCaptureAsync(...)` is implemented, alongside streamed `RunAsync()`. The example parses a child's JSON using a
typed result with non-null `CapturedStream` objects for `StandardOutput` / `StandardError`, avoiding `Capture!`.
Each stream's `Text` / `Chunks` shape and the shared observation-order counter are agreed. The
[capture design](process-capture.md) records the preferred typed failure callback and agreed explicit positive per-stream
UTF-16 limits, overflow shutdown/settlement, surrogate-pair-safe retention, and capture-limit exception properties with
bounded partial streams. Overflow/timeout/cancellation arbitration, immediate validation, per-call snapshots, and
callback/`OnFailure` selection are agreed, including capture-specific timeout/cancellation exceptions with bounded
partial streams and secondary overflow metadata. The [final decisions](process-capture.md#final-decisions-before-the-implementation-issue)
accept result inheritance, immutable public data types, a single public capture budget, and deferral of public partial
text on uncertain settlement. [Issue #44](https://github.com/sotsera/rafter/issues/44) implements this Windows slice;
the [capture guide](../guides/process-capture.md) records usage and verification scope. Binary capture, stream selection,
live parsing, spooling, custom encodings, and Linux-native ownership remain separate work.

### Completion of owned process scopes

Agreed direction: Rafter is for fast, finite scripts that finish their work and terminate. Processes started through
Rafter must not leave owned helpers running after their scope completes. Persistent helpers require a different
lifetime model outside this scope; a background-service API is not a requirement for the initial project.

- When the main process exits, preserve its observed exit code and shut down any remaining owned descendants.
  Use the same graceful-shutdown, five-second grace, and forceful-termination sequence described below, while
  continuing to consume output.
- Confirm the owned scope has stopped and owned output work has completed before returning a process result or
  applying the rejected-exit policy. Forceful cleanup alone does not invalidate an otherwise accepted root exit code
  when cleanup and output handling succeed.
- Report **Termination unknown** when Rafter cannot confirm that the owned scope has stopped. This describes missing
  confirmation, not proof that processes are still running. Retain any observed main-process exit code as diagnostic
  information; it does not establish successful completion of the whole scope.
- If process termination is confirmed but output completion is not, report **Output incomplete** separately. Do not
  describe confirmed process termination as unknown merely because output delivery could not be completed or verified.
- Neither diagnostic represents successful completion or a clean cancellation. They do not fabricate a completed
  process result, invoke the rejected-exit handler, or release Rafter from its ownership obligations. The verification
  budget and invocation outcome follow the policy below; detailed cleanup-error propagation remains open.

### Awaiting process operations

Agreed policy: combine a focused analyzer with runtime tracking of process operations in their owning command body
or target callback. Ignoring a completed process result is valid; discarding the task that represents the operation is not.

The initial analyzer reports an **error** for a clearly discarded Rafter process task, including an explicit discard:

```csharp
process.RunAsync();     // Error: task ignored.
_ = process.RunAsync(); // Error: task explicitly discarded.

await process.RunAsync();              // Valid: await completion, ignore the result.
var result = await process.RunAsync(); // Valid: await completion and use the result.
```

- Returning the task to an awaiting caller is valid, including returning it as the asynchronous body task.
- Storing a task and awaiting it later, including through an awaited `Task.WhenAll`, is valid. Starting multiple
  processes this way does not introduce parallel target scheduling; the target graph remains sequential.
- Begin with cases that analysis can prove. Do not report speculative errors for tasks passed through helpers or
  complex control flow. This is not a guarantee of detecting every missing await.
- C# already provides [CS4014](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/async-await-errors#async-method-warnings)
  for some unawaited calls. The Rafter diagnostic expresses its specific ownership requirement and covers explicit
  discards; exact diagnostic identifiers and code-fix coverage remain implementation details to design.

At runtime, track every started process operation in its owning body or target. If the callback finishes while any
of its process operations remain outstanding:

1. Report a clear execution error identifying the owning body or target and the unfinished process work.
2. Prevent later targets from starting.
3. Shut down outstanding owned processes and settle output/cleanup using the agreed shutdown policy.
4. Report invocation exit `1`, preserving **Termination unknown** or **Output incomplete** diagnostics when applicable.

Track the complete operation, including descendant cleanup and output handling, not just the main process's running
state. The runtime check detects unfinished operations; it cannot prove that an already-completed task was awaited.
It therefore complements the analyzer rather than replacing it. The runtime tracker is implemented and verified in
the [process slice](../guides/process-runtime.md); [RAF003](../guides/authoring-diagnostics.md#raf003) implements the initial analyzer coverage.

### Cancellation and owned child processes

Agreed cancellation baseline:

- Ctrl+C requests invocation cancellation and prevents later targets from starting.
- Expose `context.CancellationToken` for cooperative script work, such as
  `await Task.Delay(delay, context.CancellationToken)`.
- Processes started through that context automatically participate in invocation cancellation.
- Wait for running callbacks and owned process/output work to settle before reporting completion. A callback that
  ignores cancellation may delay this; cancellation does not forcibly stop arbitrary managed callback code.
- Report **Cancelled** with exit `130` when cleanup succeeds and no unhandled or recorded execution failure remains,
  rather than classifying expected invocation cancellation as an unexpected exception.
- Once invocation cancellation is requested, catching a cancellation exception cannot turn the invocation into success.
  Catching remains useful for cleanup. `OnFailure` does not handle cancellation.

Agreed outcome precedence when cancellation overlaps with failure:

- Cancellation with successful cleanup and no unhandled or recorded execution failure produces **Cancelled**, exit `130`.
- An unhandled execution failure or an already recorded `ProcessFailureAction.Fail` produces **Failed**, exit `1`,
  even if cancellation is also requested. Preserve the original failure diagnostic; cancellation must not hide it.
- A failure already handled through `Continue` or a deliberately caught process exception remains handled. It does not
  become an invocation failure merely because cancellation follows. Resource-ownership obligations still apply.
- **Termination unknown** or **Output incomplete** produces exit `1`, with the specific status clearly visible,
  including when cancellation was requested. Preserve any original execution failure alongside the cleanup diagnostic.
- Errors caused by expected invocation cancellation, such as a cancelled output read, do not automatically count as
  independent execution failures. Cancellation still requires shutdown verification and completed output handling;
  if output remains incomplete, report that condition with exit `1`.

These rules select the reported outcome; they do not permit abandoning Rafter's own readers or cleanup operations.

Child shutdown sequence accepted for now:

1. Establish ownership when each process is launched, covering the child and descendants in its OS-managed scope.
   Do not wait until cancellation to discover a tree of process IDs. Unrelated processes, pre-existing services, and
   work delegated through external service brokers are outside that ownership; do not kill by executable name.
2. On cancellation, stop admitting new process launches and request graceful shutdown where a supported mechanism
   can target only the owned scope. Continue consuming output during shutdown.
3. Allow a shared five-second grace period for the affected scopes, not five seconds per descendant. If scoped graceful
   signalling is unavailable, proceed directly to termination rather than waiting without having sent a request.
4. Force termination of remaining owned processes after the grace period. Allow a separate shared five-second budget
   for termination verification and output draining across all affected scopes, not five seconds per process or stream.
   Confirm that the scopes have no running processes and finish owned output delivery. Grace expiry starts escalation;
   it does not prove shutdown has completed.
5. A second Ctrl+C skips the remaining grace period and requests forceful child termination. It does not declare
   completion or abort managed callbacks. Preserve the verification/drain budget; repeated interrupts do not restart it.
6. If the verification/drain budget expires without confirmation, report **Termination unknown**; if only output
   completion is unconfirmed, report **Output incomplete**. Both use exit `1` under the agreed precedence.
   Detailed escalation-error handling remains to be designed.

Both five-second budgets are initial defaults and maximum waits, not fixed delays. Proceed as soon as each phase
finishes. They also apply when cleaning up surviving descendants after a normal main-process exit. These budgets
bound the graceful-shutdown and verification/drain phases, not total invocation duration: arbitrary script callbacks
that ignore cancellation may still delay `RunAsync`. Rafter must settle its own readers and cleanup operations before
returning; deadline expiry alone does not make those operations safe to abandon. How to interrupt pending I/O and
settle those operations safely is an implementation requirement to verify, not a claim of a ten-second return guarantee.

Implementation direction to investigate, not a platform-support commitment. See the pinned
[Aspire shutdown review](../history/aspire-process-shutdown-review.md) for its current implementation and differences from Rafter's
contract; that research does not automatically adopt Aspire's policies or dependencies.

- On Windows, use a dedicated Job Object per process execution with breakaway disabled and kill-on-close protection.
  Establish membership at creation, before child code runs; retain the controlling handle until shutdown verification
  and owned output settlement finish. If ownership setup fails, fail the launch rather than silently weakening the contract.
- A Windows console group can support a targeted Ctrl+Break request when console requirements are satisfied. Do not
  broadcast Ctrl+C to unrelated processes sharing the terminal. Some tools provide no useful graceful shutdown protocol;
  the fallback remains scoped forceful termination.
- Validate other operating systems separately before claiming equivalent containment. Process groups alone should not
  be described as inescapable ownership of every possible descendant. Public APIs need not expose Windows Job Objects.

Research basis, checked 2026-10-19:

- [.NET Process.Kill](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0):
  `Kill(entireProcessTree: true)` followed by `WaitForExit` does not verify that all descendants have exited.
- [Windows Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects): process grouping,
  inherited membership with documented exceptions, termination, and kill-on-close behavior. Job notifications alone
  are not universally guaranteed; verification must establish actual settlement.
- [Creating a process in a job](https://devblogs.microsoft.com/oldnewthing/20230209-00/?p=107812): creation-time job
  assignment avoids the gap between launching a process and placing it under ownership.
- [Console control events](https://learn.microsoft.com/en-us/windows/console/generateconsolectrlevent): Ctrl+C cannot
  target a specific process group; Ctrl+Break targeting has shared-console restrictions.

Still discuss cleanup-error propagation and external
cancellation-token entry points. These policies are not inferred from the old implementation.
Windows ownership and real-process verification are recorded in [process-runtime.md](../guides/process-runtime.md).

### Targets and dependencies

Scenario: build a project and then run its tests through a dependency-only grouping target. The runnable Windows example is
[examples/dependencies.cs](../../examples/dependencies.cs). The [callback-based target slice](../guides/targets.md) implements
sequential graph execution, also demonstrated by [examples/targets.cs](../../examples/targets.cs). External-process
integration is verified by the [process slice](../guides/process-runtime.md); empty-target analyzer coverage remains pending.

Agreed execution shape:

- Declare targets on the command with `command.Target(name, description)` and register their bodies with `.Run(...)`.
- `.DependsOn(build)` expresses a prerequisite; in the example, build completes successfully before test runs.
- Select entry targets with `command.RunAsync(args, test)` or multiple targets through the overload requiring one
  target followed by a `params` array of additional targets.
- There is no stored command body to run after the selected targets. A callback passed to the body overload is an
  alternative execution form, not an additional workflow step.
- Failures prevent further target scheduling, with the current body's continuation governed by the agreed failure policy.

Agreed ordering and deduplication:

- Execute targets sequentially. Visit selected targets in the order supplied to `RunAsync`; for each target, visit
  its dependencies in their supplied order before executing that target.
- Dependency prerequisites take precedence over the selection order. Execute each target at most once per invocation,
  including dependencies shared by several targets and targets also listed explicitly as entries.
- For `test.DependsOn(build)`, all three selections below produce `build`, then `test`, assuming success:

```csharp
return await command.RunAsync(args, test);
return await command.RunAsync(args, build, test);
return await command.RunAsync(args, test, build);
```

These are alternative invocations. In the last one, visiting `test` first visits and executes `build`; after `test`
finishes, the explicit `build` entry is already complete and is not executed again. A recorded failure prevents
scheduling the remaining work.

Agreed grouping and empty-target rules:

- A target can group dependencies without declaring a body. For example,
  `command.Target("verify", "Build and test the project.").DependsOn(build, test)` is valid.
- A target with neither a body nor dependencies is invalid. Initial `RunAsync` validation reports the declaration
  error and returns `2` before executing work callbacks. Calling `.DependsOn()` with no targets does not supply a
  dependency and does not make an otherwise empty target valid.
- Add analyzer feedback for empty targets where static analysis can reliably establish the missing body and
  dependencies at invocation. Do not diagnose the initial `Target(...)` call just because configuration is supplied
  later through `.Run(...)` or `.DependsOn(...)`.
- Handle straightforward local configuration where feasible. If aliases, helper methods, or control flow make the
  final declaration uncertain, avoid speculative diagnostics and rely on initial runtime validation. Analyzer feedback
  complements runtime validation; it does not replace it. Exact coverage and severity remain to be settled during
  analyzer design and verified with valid configuration patterns as well as invalid ones.

Agreed graph validation:

- Validate all declared targets during initial `RunAsync` validation, including targets not reachable from the selected
  entries. This declaration validation also applies to the body overload and to help; unused declarations do not bypass it.
- Reject dependency cycles, including self-dependencies, and show the cycle path in the diagnostic, such as
  `build -> test -> build`.
- Reject selected targets or dependencies belonging to another command, null target/dependency entries, and duplicate
  target names within the command. Graph declaration errors return `2` before work callbacks execute.
- Repeated references to the same target are harmless in selections and dependency lists. Retain the first occurrence's
  traversal position and execute each target only once; this differs from distinct targets sharing a duplicate name.
- Only the selected targets and their reachable dependencies execute. Validating an unused target does not schedule it.

Agreed nonempty invocation contract:

- The target overload is `RunAsync(string[] args, Target target, params Target[] additionalTargets)`. Requiring the
  first target makes `RunAsync(args)` a compile-time error without needing an analyzer for that omission.
- Every invocation supplies either a synchronous/asynchronous body or at least one target. A target-array-only
  overload that could accept an empty selection is not part of this design.
- Help uses the same invocation shapes: the script supplies its body or targets, and `--help` describes the invocation
  without executing its work. There is no separate no-work invocation form for help.
- Compiler enforcement of argument presence does not replace runtime validation of nulls, ownership, and graph validity.

The target and dependency design above is agreed. Callback-based graph execution is implemented and verified in the
[target runtime record](../guides/targets.md); the build/test workflow is verified in the [process record](../guides/process-runtime.md).
Analyzer coverage remains pending.

The [agreed conditional-target extension](#conditional-targets--agreed-behavior-2026-10-28) adds `.When(...)` and
body-only skips to this baseline under #66. Dependency-first ordering is preserved; skipped prerequisites allow
dependants to continue, while failure and cancellation stop scheduling.

### Automatic namespace import

The package can supply an MSBuild `Using` item for `Sotsera.Rafter`, generating a global namespace import so consumer
scripts do not need an explicit `using Sotsera.Rafter;`. The SDK documents [Using items](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#using),
and NuGet supports [package-supplied props and targets](https://learn.microsoft.com/en-us/nuget/concepts/msbuild-props-and-targets).

Initial package behavior:

- Enable the namespace import for direct C# consumers of the Rafter package, with `RafterImplicitUsings=false` as the opt-out.
- Use package build assets rather than a source generator. Consider a `.targets` file for adding the conditional
  `Using` item after consumer properties are evaluated; a `.props` file can supply default properties if needed.
- Keep the import scoped to direct package consumers rather than automatically propagating it through transitive
  dependencies. Local project references need their own development setup because they do not consume the packaged assets.
- Verify actual file-based apps, regular C# project consumers, design-time assets, and the opt-out using a built package.
  File-based apps use a generated project, as described in the [SDK guide](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps),
  and the package mechanism is now exercised by isolated consumer checks. Live VS Code diagnostics and completion are
  verified in the [milestone review](../history/milestone-review.md).

The [local package](../guides/local-package.md) implements this default; development examples use equivalent build assets and
omit the explicit imports. Namespace imports do not flow through transitive project references. Public publication
and editor-host compatibility beyond the recorded VS Code baseline remain separate gates.
