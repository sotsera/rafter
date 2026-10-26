# CI duration and suite scheduling

The [cache-placement experiment](ci-cache-measurements.md) improved root restore but did not improve complete Windows
job time. This follow-up profiles the full suites and tests a smaller critical path while preserving coverage.
It is tracked by [issue #58](https://github.com/sotsera/rafter/issues/58).

## Baseline and scheduling change

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

Three complete normal evaluations and one legacy-mode control completed at
`555c908cfa61f4fcc214e6f68fc2d39fec9e4ca4`. All passed. No failed attempt was excluded or replaced. Windows used SDK
**10.0.403** and image **20261022.344.1**; Linux used the same SDK and image **20261012.334.1**.

| Run | Complete workflow | Windows coverage ready | Summed Windows runner time |
| --- | ---: | ---: | ---: |
| [Parallel sample 1](https://github.com/ghidello/rafter/actions/runs/40406021026) | 5m38s | 5m37s | 10m10s |
| [Parallel sample 2](https://github.com/ghidello/rafter/actions/runs/40406025188) | 6m00s | 5m59s | 9m20s |
| [Parallel sample 3](https://github.com/ghidello/rafter/actions/runs/40406030681) | 5m06s | 5m06s | 9m49s |
| [Same-revision serial control](https://github.com/ghidello/rafter/actions/runs/40406035931) | 8m56s | 8m56s | 8m52s |

The parallel median was **5m38s** (range **5m06s–6m00s**) for the complete evaluation, including all three jobs. This
was **3m18s / about 37% shorter** than the same-revision serial control, and **1m22s / about 20% shorter** than the
latest merged-main run at 7m00s. The serial control is a single sample and skips Linux, while normal runs include
it; Linux completed before Windows in all three parallel samples. The earlier three default-cache Windows samples
were 7m08s–8m08s. These comparisons show a shorter observed critical path, not a universal percentage guarantee.

The tradeoff is extra setup/restore/build on a second Windows runner. Median **summed Windows runner time was 9m49s**
(range **9m20s–10m10s**) against **8m52s** for the same-revision serial control, an increase of **57s / about 11%**.
The earlier merged-main Windows job was 6m57s, illustrating runner variability and a larger increase against that
faster baseline. Runner time and wall time are different measurements; this change prioritizes review latency.

Every parallel sample passed **710 portable Windows cases**, **96 native Windows cases with 1 console-dependent
skip**, and **710 Linux portable cases**. Packaging and all **43** redirected checks passed. For each sample, the
combined Windows TRX matched **all 740 ordinary and 807 full case identities and outcomes** against the retained
pre-split baseline. The serial control also matched both complete Windows profiles, verifying that the existing
cache experiment retained its original selection. No test source, project wiring, timeout, assertion, or cache policy
changed to obtain these results.

**Decision: retain the split for review.** The repeated complete-run result supports the latency improvement, with
the explicit runner-time tradeoff above. All three normal check results matter; a single green Windows check no
longer represents the complete Windows suite. Final PR-revision CI is recorded separately in the PR description.

Raw run/job metadata, logs, TRX artifacts, and comparison data are retained under `artifacts/evidence/ci-profile/`.
`samples.json` records the four run IDs, modes, and source revision; `observations.json` records timings and outcomes.
Complete workflow time is provider `updated_at - created_at`, Windows readiness is the last Windows completion minus
workflow creation, and summed Windows runner time adds the two Windows job intervals. Hosted-runner variability and
the small sample count limit generalization. Local test runs do not establish hosted duration.
