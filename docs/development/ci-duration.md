# CI duration and suite scheduling

The [cache-placement experiment](ci-cache-measurements.md) improved root restore but did not improve complete Windows
job time. This follow-up profiles the full suites and tests a smaller critical path while preserving coverage.
It is tracked by [issue #58](https://github.com/sotsera/rafter/issues/58).

## Baseline and proposed change

The [PR #57 Windows job](https://github.com/sotsera/rafter/actions/runs/40400873278/job/121846050625), at `99a2d71`,
took **7m32s**. Full tests ran serially by module: unit **2.83s**, integration **148.58s**, Windows **195.26s**.
The full step took **348.79s**. Its TRX reports contain 664 unit, 46 integration, and 97 Windows cases (807 total).
The longest integration case was the fresh package consumer at 73.02s. Real SDK consumer builds account for much of
the work; dropping real consumers or weakening their assertions would change the verification contract.
That commit's Git tree is identical to merged main `0f77773`; its
[post-merge evaluation](https://github.com/sotsera/rafter/actions/runs/40403934136) passed with a 6m57s Windows job
and 7m00s complete workflow. These are individual baseline runs, not an average.

Run the existing portable unit/integration suites in a new **Windows portable evaluation** job on its own runner,
concurrently with **Windows evaluation**. Normal Windows evaluation runs only `Rafter.WindowsTests`, then retains
runtime/analyzer packaging and redirected checks. Linux portable evaluation keeps its existing scope. Keep ordinary
and full profiles, one test module at a time within each job, and all native nonparallel collections/deadlines.
The projects, solution/filter memberships, runtime code, and test source remain unchanged.

Separate jobs provide separate operating-system processes, consumer workspaces, caches, and native resources. They
avoid overlapping SDK-heavy portable consumers with short-deadline Windows probes on the same machine. GitHub's
[job scheduling](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-jobs)
supports independent jobs; no `needs` dependency is introduced between these checks.

The tradeoff is a second Windows restore/build/setup and potentially more aggregate runner time. Measure the full
workflow and Windows coverage completion time, plus summed Windows job time; do not report a shorter individual job
as an improvement if the complete checks take longer. Preserve the main/PR/manual triggers and cancellation policy.
No branch protection change is included; the inspected `protect-main` ruleset is disabled. Review all three jobs.

## Coverage and benchmark compatibility

| Normal job | Ordinary cases | Full cases | Artifact |
| --- | ---: | ---: | --- |
| Linux portable evaluation | 680 | 710 | `linux-portable-evaluation` |
| Windows portable evaluation | 680 | 710 | `windows-portable-evaluation` |
| Windows evaluation | 60 | 97 | `windows-evaluation` |

Combined Windows coverage remains 740 ordinary / 807 full cases. One native case requires an attached console and
is expected to skip on hosted runners. Require nonempty discovery in each job and compare the full Windows TRX
case identities with the retained pre-split reports, not just their totals.

The manual `nuget-cache-experiment=default` or `runner-temp` mode retains its original single Windows job and full
`Rafter.slnx` selection. Both portable jobs skip in that explicit mode, so the cache experiment still measures the
same complete Windows workload. Manual `off`, PR, and main evaluations use the new split. Cache placement remains
unchanged following the six-sample result.

## Verification and measurements

Local ordinary selections passed **680 portable cases** and **59 Windows cases with 1 console-dependent skip**.
The combined 740 case identities and outcomes exactly match the retained pre-split ordinary TRX. Project/reference
and solution/filter inspection required no wiring edits. Actionlint and documentation/parser checks passed.

Hosted measurements are pending. Collect three complete normal evaluations at one revision and one legacy-mode
run to verify cache-experiment compatibility. Retain the results and
retain run/job metadata, logs, and TRX under `artifacts/evidence/ci-profile/`. Compare test identities for both ordinary
and full profiles with the baseline. Report actual SDK/image, all job outcomes, wall time, and runner-time tradeoff;
include failed attempts and explain any exclusions. Local checks do not establish hosted-runner duration.
