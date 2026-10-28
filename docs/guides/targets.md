# Sequential targets and dependencies

Issue [#13](https://github.com/sotsera/rafter/issues/13) implements callback-based target graphs on the typed-input
runtime. [targets.cs](../../examples/targets.cs) prepares a greeting and then prints it through a dependency-only group.
The [process slice](process-runtime.md) now runs the [dependencies.cs](../../examples/dependencies.cs) build/test workflow
on Windows and stops later targets after an unhandled process error, recorded `Fail`, or unfinished process work.

## Authoring and execution

Declare a target with `command.Target(name, description)`. Register one synchronous `Action<RafterContext>` or
asynchronous `Func<RafterContext, Task>` body with `.Run(...)`. `.DependsOn(params Target[] dependencies)` appends
prerequisites. A target with dependencies can omit its body to form a group. A target with neither is invalid.

Select targets with `RunAsync(string[] args, Target target, params Target[] additionalTargets)`. The first target
is required by the compiler. There is no array-only invocation overload and no invocation combining a command body
with targets. Existing body overloads still validate all declarations but execute only their supplied body.

Targets run sequentially in depth-first dependency order. Entry order and dependency declaration order break ties;
prerequisites always precede dependants. For `test.DependsOn(build)`, selecting `test, build` executes `build`, then
`test`. Shared dependencies and repeated references execute once per invocation. Duplicate references retain their
first traversal/display position. Unselected, unreachable targets never execute.

Both bodies and groups participate in planning, but only bodies produce execution sections and completion counts.
An asynchronous body is awaited before the next body starts. A null returned task, an unhandled exception, or an
output failure stops all later scheduling and returns 1. Catching a writer exception does not clear the recorded
output failure. Ordinary user exceptions deliberately caught by the body remain handled under the existing policy.

Ctrl+C cooperatively cancels the invocation. The current body must settle, including its awaited cleanup, before
the invocation completes. Cancellation is checked before scheduling and after each body; catching cancellation
inside a body does not permit the next target to start. Clean cancellation returns 130. Independent failures,
including cancellation-callback or output errors, take precedence and return 1. No forceful managed-body shutdown
or external-process ownership is implemented in this slice.

## Validation and snapshots

Initial validation examines every declared target, including unused declarations, for all invocation forms and help.
It runs before parsing, input validators, or work callbacks and returns 2 for:

- Invalid or duplicate target names, or null/blank descriptions. Names use the shared lowercase ASCII name grammar
  without option prefixes; targets have a separate namespace from options and may use names such as `help`.
- Empty targets, null bodies, or multiple `.Run(...)` calls. Fluent calls collect errors without throwing them.
- Null dependency arrays or entries, dependencies from another command, or null/foreign selected targets.
- Dependency cycles, including self-dependencies, with a diagnostic path such as `build -> test -> build`.

All graph declarations, body delegates, dependency lists, and selected entries are copied at invocation entry alongside
input declarations. Callback mutations affect later invocations; they do not alter the captured plan. Concurrent
declaration mutation is not a supported authoring pattern. Separate invocations have their own plans, prepared values,
completion counts, cancellation, and output state. No completed flag is stored on a reusable target declaration.

Graph validation and planning use explicit stacks, so deep valid graphs do not consume the managed call stack.
The empty-target analyzer and other authoring diagnostics remain deferred; runtime validation is implemented now.

## Help and output

Help describes the script-selected graph without executing input rules or bodies. It lists entry names, distinguishes
groups from bodies, shows each reachable target once with direct dependencies, and states actual body order. It does
not introduce CLI target selection. Malformed supplied arguments still fail with help; omitted required inputs do not.

Lifecycle output goes to stderr. A single grouping entry appears as `> verify <-- (prepare, greet)` with the command
description. Multiple entries use their selection order in the heading and summary. Body headings follow execution
order, use `[greet] <-- (prepare)`, and put descriptions at column zero with a blank line before content. Dependency
suffixes are muted in rich terminals. Each successful body has its own elapsed time excluding dependencies. One final
summary gives selected entry names, completed-body count, and total invocation time; failures identify the target
where execution stopped when one was active. Groups have no empty execution section or extra completion status.

Quiet hides routine headings and successful statuses while retaining script output and failures. Plain/redirected
stdout preserves script text without styling or presentation prefixes. Existing stream-specific color/NO_COLOR
policies also apply to target output. Host writers remain open until invocation output settlement completes.

## Conditional target bodies

The local [#66 increment](https://github.com/sotsera/rafter/issues/66) adds a synchronous, described body guard:

```csharp
var pack = command.Target("pack", "Create local packages.")
    .DependsOn(test)
    .When("Packaging was requested with --pack.", context =>
    {
        return context.GetValue(createPackages);
    })
    .Run(async context =>
    {
        await context.Dotnet().UseWorkingDirectorySdk()
            .Pack("src/Sotsera.Rafter/Sotsera.Rafter.csproj")
            .Configuration("Release")
            .RunAsync();
    });
```

`When(string description, Func<RafterContext, bool> predicate)` also accepts an expression lambda such as
`context => context.GetValue(createPackages)`. A block lambda requires an explicit Boolean `return`.
The description states the condition and appears in help and skip output.

Evaluate a condition once per reachable target per invocation, after its dependencies settle and before its body
would start. False skips only the body: prerequisites have already run and dependants may continue. Shared targets
are visited once; later invocations evaluate them anew. Fluent declaration order does not change timing. Conditions
are included in declaration snapshots. A dependency expresses ordering, not proof that tests ran or an artifact exists.

The [conditions.cs example](../../examples/conditions.cs) builds/tests by default and skips packaging. `--pack` opts
into packages; `--skip-tests` disables tests. Both flags deliberately build, skip tests, and pack. See the
[agreed behavior matrix](../design/example-design-plan.md#conditional-targets--agreed-behavior-2026-10-28).

Predicate exceptions fail that target and stop later scheduling. Cancellation also stops scheduling, including when
a predicate requests cancellation and returns false. Existing failure/cancellation precedence is preserved. Earlier
unhandled prerequisite failures prevent later predicates from running. Predicates receive invocation context outside
a body and cannot launch body-owned processes; use them for prepared inputs/already available state, and bodies for
asynchronous work.

Initial validation rejects null predicates, blank descriptions, repeated `When` calls, and conditions on dependency-only
groups, even when unused or invoked with help/a command body. False conditions cannot hide invalid graphs or inputs.
Combine checks with ordinary C# `&&`/`||`. Async predicates, conditional groups, branch pruning, and cleanup hooks remain
outside this increment.

Skipped bodies emit a literal lifecycle line on stderr, such as
`[pack] Skipped: condition not met (Packaging was requested with --pack.)`. There is no body-start section, completion
status, or fabricated body duration. Successful summaries append a separate skipped count when nonzero:
`2 target bodies completed, 1 skipped`. Groups add no body counts. Quiet hides routine skips and successful summaries
while retaining script output and failures. Help shows `When:` descriptions and planned body order without invoking
predicates or claiming a runtime outcome.

## Conditional verification, 2026-10-28

The #66 increment passed a full local Windows regression: **899 passed, 0 failed, 1 existing console-dependent
skip** across 900 cases (unit 726; integration 51; Windows 123). Release solution/runtime-consumer builds have
0 warnings/errors, and all 43 redirected runtime cases passed. Thirty new unit cases cover skip, validation, help,
ordering, snapshots/isolation, failures/cancellation, and output behavior. Real file-app/project/package consumers
exercise the API; the actual example runs all four combinations and prerequisite failure on a disposable real
build/test/pack solution. Evidence is retained under `artifacts/evidence/conditional-targets/` and
`artifacts/test-results/conditional-full/`. Local review on 2026-10-28 found no blocking code issues. PR/main CI,
new Linux checks, attached-console, live-editor, macOS, and AOT checks are not claimed for this increment.

## Invocation-local state and cleanup

The local [#68 increment](https://github.com/sotsera/rafter/issues/68) adds typed state shared by callbacks within
one invocation, plus cleanup for an entered target body or the complete command:

```csharp
var resources = command.State(() => new WorkflowResources());

command.Cleanup("Release shared resources.", context =>
{
    var state = context.GetState(resources);
    // Release the resources this invocation recorded in state.
});

var prepare = command.Target("prepare", "Prepare workflow resources.")
    .Run(context =>
    {
        var state = context.GetState(resources);
        // Acquire resources here, recording ownership before later work can fail.
    })
    .Cleanup("Release preparation resources before dependants.", async context =>
    {
        var state = context.GetState(resources);
        // Await asynchronous disposal here.
        await Task.CompletedTask;
    });
```

The full [cleanup.cs example](../../examples/cleanup.cs) creates a shared workspace/input and two target-local scratch
files. Verification keeps a stream open until its async cleanup disposes it. `finish` reads the shared input after
target cleanup; command cleanup then removes the disposable workspace. It exposes `--skip-verification`, `--fail`,
and `--fail-cleanup` to demonstrate eligibility and combined errors. Standard `System.IO` handles reads/deletion.
See the [agreed plan](../design/example-design-plan.md#cleanup-and-shared-resources--agreed-behavior-2026-10-28).

### State ownership

`command.State<T>(Func<T> factory)` returns a `State<T>` handle for a reference type. The handle is a typed key, not
the object or its resource owner. Rafter calls every declared factory once after input validation and root selection,
before entering normal work. `context.GetState(handle)` retrieves that invocation's value without calling the factory
again. Bodies, predicates, and cleanup see the same value; repeated or overlapping invocations have separate storage.
Factories must return fresh nonnull objects, allocate only state, and acquire no resources. Rafter does not clone
caller objects or automatically dispose state; resource acquisition/release stays explicit in bodies/cleanup.

Null factories and Task-valued state declarations are rejected before help/work. Factory exceptions, null results,
and Task results hidden behind object/base return types fail initialization with exit 1 before any body or cleanup
enters. Help and invalid inputs/declarations do not initialize
state. Foreign handles, including declarations added after an invocation's snapshot, cannot be read from that
invocation. Factories and cleanup declarations are snapshotted before input callbacks.

### Cleanup boundaries

Both `command.Cleanup` and `target.Cleanup` accept a required description plus either `Action<RafterContext>` or
awaited `Func<RafterContext, Task>`. Only one callback per owner is supported. Null callbacks, blank descriptions,
duplicates, and cleanup on dependency-only groups are declaration errors, including unused targets and help.

Target cleanup becomes eligible immediately before the body is entered after a true condition. It runs once after
that body and its owned processes/output settle, including body failure, partial acquisition, or cancellation.
Unselected, unreachable, skipped, or predicate-failed targets never enter cleanup. Target cleanup completes before
dependants start; cleanup failure stops normal scheduling. Command cleanup is still attempted after such failures,
once all entered work and target cleanup have settled. It also supports ordinary command-body invocations. No cleanup
runs for help, invalid declarations/inputs, cancelled initialization before work entry, or failed state initialization.

Cleanup receives the same root, prepared values, and state, with `CancellationToken.None` and no executing process
body. It cannot launch children through Rafter or reuse an executable whose original body has closed. Work cancellation
does not cancel cleanup; await actual disposal/cleanup completion. There are no cleanup deadlines or forceful managed
termination in this scope. A callback that does not settle keeps its invocation running. Scope and dispose work's
cancellation registrations inside the body; detached work is not made safe by cleanup. Active cancellation callbacks
are joined before cancelled-work cleanup, and the command cancellation scope is closed/joined before command cleanup
so late interrupts cannot start old work callbacks against released shared resources.

If process termination is uncertain, resource cleanup is not safely attempted: retain resources and report that
diagnostic rather than deleting files potentially used by a live child. Verified process exit with an output failure
does not itself mean uncertain native termination. Existing process ownership/reader settlement remains authoritative.

Body and cleanup errors are retained separately with owner/description; a cleanup error never replaces the body
exception. Success is 0; clean work cancellation with successful cleanup is 130; an independent work/cleanup error
is 1; invalid declarations/inputs remain 2. A null cleanup Task is a cleanup failure. Lifecycle stderr distinguishes
cleanup from normal body completion/skip counts. Quiet hides routine `[Cleanup]`/`[OK] Cleanup` statuses while retaining
script output and failures. Help describes cleanup without invoking factories or callbacks. A failing cleanup heading
does not prevent the resource-release callback from being attempted when process settlement is safe.

### State/cleanup verification, 2026-10-28

The final full local Windows regression passed **949 tests, 0 failures, 1 existing console-dependent skip**, across
950 cases (unit 772; integration 53; Windows 125). Release solution/runtime-consumer builds have 0 warnings/errors;
all 43 redirected runtime cases passed. Forty-six new unit cases cover the state/cleanup contracts. Two real consumers
execute the actual example as a file app and ordinary project, including seven option combinations/help. Existing
fresh package consumers exercise both cleanup scopes and shared state. Two native cases verify cleanup after a real
child and its output settle, including abandoned work; controlled cases retain resources after uncertain termination.
Separate occurrences retain cleanup diagnostics even if user code rethrows the same body exception object.

Final reports are in `artifacts/test-results/state-cleanup-full-final/`; raw/earlier logs remain under
`artifacts/evidence/state-cleanup/`. Only whitespace changed in the unit file after that run, followed by a clean
build. These are historical implementation results; existing conditional-target results above also describe their
original revision.

### State/cleanup review, 2026-10-29

Review added initialization-time rejection of Task results hidden behind object/base-typed state factories, with a
regression confirming no work or cleanup is entered. No other blocking code findings remained. The reviewed full
Windows suite passed **950 tests, 0 failures, 1 existing console-dependent skip**, across 951 cases (unit 773;
integration 53; Windows 125). Release builds have no warnings/errors, and all 43 redirected runtime checks passed.
Reports are under `artifacts/test-results/state-cleanup-review-full/`, with logs under
`artifacts/evidence/state-cleanup/review-20261029/`. PR/main checks and user review remain delivery checkpoints;
Linux-native ownership, attached-console/live-editor, macOS, and AOT are not established by these local results.

## Historical verification of the initial target slice

[Target runtime tests](../../tests/Rafter.UnitTests/Targets/TargetTests.cs) exercise order, shared dependencies, repeated
references, nested groups, a 12,000-level graph, all-declaration validation, cycles, ownership, awaited execution,
failure/cancellation cleanup, snapshots, overlapping invocations, input integration, and 48/100-column help.
[Consumer tests](../../tests/Rafter.IntegrationTests/Consumers/TargetConsumerTests.cs) build and run the actual targets.cs and check
unsupported invocation shapes at compile time. Existing [runtime consumers](../../tests/testassets/consumers/runtime/Scenario.cs) also
exercise successful and failed target workflows in both file apps and ordinary projects with warnings as errors.

Local verification on 2026-10-21 used Windows x64, SDK 10.0.402, and managed net10.0 execution. The full native-MTP
suite reports **Passed: 287, Failed: 0, Skipped: 0**: 56 additional cases plus extended existing consumers. The Release
build reports **0 warnings and 0 errors**. Commands from the repository root use the current project path;
the recorded results predate the repository reorganization:

```powershell
dotnet build Rafter.slnx --configuration Release --no-restore -bl:artifacts/build.binlog
dotnet test --solution Rafter.slnx -p:IncludeLongRunningTests=true --max-parallel-test-modules 1 --configuration Release --report-trx --results-directory artifacts/test-results
```

Manual Windows pseudoterminal runs of targets.cs and its help confirmed bold target/group headings, muted direct
dependency suffixes, literal result prefixes, flat descriptions, per-body timings, and one final group summary.
Automated redirected captures confirm clean stdout and lifecycle stderr; the narrow help checks use 48 columns.
CI evidence is recorded in the implementation PR.

At that initial source revision, external-process execution/settlement, ProcessFailureAction, analyzers, parallel
targets, conditional execution, packaging, and Linux/macOS verification remained separate work. Later delivered
capabilities are recorded in the development plan; conditional behavior is described above. No dependency versions
changed in the initial target slice.
