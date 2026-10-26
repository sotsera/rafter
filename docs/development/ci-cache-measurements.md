# Windows NuGet cache measurements

The cache-placement experiment follows the scheduling changes merged in [PR #55](https://github.com/sotsera/rafter/pull/55).
It tests the [cache-placement article's](https://awakecoding.com/posts/speeding-up-dotnet-builds-on-windows-nuget-cache/)
suggestion on Rafter's actual Windows job before changing the default evaluation policy.
It is tracked by [issue #56](https://github.com/sotsera/rafter/issues/56).

## Method

Use the existing evaluation workflow's manual `nuget-cache-experiment` input. `off` runs the normal Windows/Linux
evaluation; `default` and `runner-temp` run only the complete Windows job. The latter sets `NUGET_PACKAGES` and
`NUGET_HTTP_CACHE_PATH` beneath `RUNNER_TEMP` before restore/build/tests. Default placement leaves those variables
unchanged. Every sample gets a fresh hosted `windows-2022` runner and checkout at the same commit.

Collect three successful samples of each placement, alternating dispatch order. Record every run ID, source revision,
SDK, runner image, resolved cache paths, step durations, and job outcome. Use medians and ranges for restore, build,
full tests, and total Windows job time. Keep failed attempts visible; replace them only for the successful-sample
comparison, and explain the exclusion. This compares the practical runner defaults against an initially empty
runner-temp cache; it does not claim a controlled disk-only or network-free microbenchmark.

The full suite matters because it restores/builds real file-app and project consumers. Run ordinary/full tests,
packaging, redirected runtime checks, and evidence upload in both placements. Existing package-consumer tests must
override the ambient `NUGET_PACKAGES` with each consumer's private `.packages` directory and assert that it starts
empty. They must still verify the freshly packed runtime and analyzers. Other consumers can share the job cache.

Keep main pushes and PR evaluations enabled. Benchmark dispatches use the existing unique manual-run concurrency
groups, so samples do not cancel each other. Normal PR/main/manual-off evaluations retain both jobs and their
existing commands. Benchmark dispatches alone skip Linux.

## Decision rule

Adopt runner-temp placement only if the repeated whole-job results support an improvement and all correctness checks
pass. Report variability and the limits of three samples; an isolated fast restore is insufficient. If the evidence
does not support a useful improvement, retain default placement and record the result.

Remote archive caching is a separate experiment: include download/extraction/save time and warm/miss behavior before
adoption. The repository has no committed lock files, and [setup-dotnet's built-in cache](https://github.com/actions/setup-dotnet#caching-nuget-packages)
requires them by default. This increment introduces neither a lock-file policy nor remote cache archives.
NuGet's [cache documentation](https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders)
describes the environment overrides used here.

## Results — 2026-10-26

**Keep default cache placement.** All six hosted samples passed, but runner-temp placement did not improve the
complete Windows job. Its median job time was **9m03s**, compared with **8m05s** for default placement: **58s longer
(about 12%)**. It was slower in each of the three dispatch pairs. Root restore improved consistently, but the full
test suite dominated the job and took longer in these samples.

All samples used commit `467c2938cf5b810cc59bf970c60ebbd304bae4d9`, SDK **10.0.403**, and runner image
**20261022.344.1** (`win22`). Each reported Windows full **806 passed, 0 failed, 1 console-dependent skip**, successful
packaging, and **43** redirected runtime checks. No failed sample was excluded or replaced.

| Metric | Default median (range), seconds | Runner-temp median (range), seconds |
| --- | --- | --- |
| Restore | 21 (16–22) | 4 (4–5) |
| Build | 9 (8–9) | 11 (9–13) |
| Full tests | 365 (332–374) | 436 (368–455) |
| Complete Windows job | 485 (428–488) | 543 (474–565) |

These are hosted-runner observations from three samples per placement, not a controlled disk-only experiment.
They do not establish why the full suite took longer or a universal regression from relocating caches. They do
establish that this measurement does not support adopting runner-temp placement for Rafter's normal evaluation.
The 17s median root-restore saving alone would overstate the practical result.

| Pair | Default run / job time | Runner-temp run / job time |
| --- | --- | --- |
| 1 | [40398812432](https://github.com/ghidello/rafter/actions/runs/40398812432) / 7m08s | [40398832438](https://github.com/ghidello/rafter/actions/runs/40398832438) / 7m54s |
| 2 | [40398868878](https://github.com/ghidello/rafter/actions/runs/40398868878) / 8m05s | [40398851587](https://github.com/ghidello/rafter/actions/runs/40398851587) / 9m25s |
| 3 | [40398885543](https://github.com/ghidello/rafter/actions/runs/40398885543) / 8m08s | [40398901559](https://github.com/ghidello/rafter/actions/runs/40398901559) / 9m03s |

Default resolved packages to `C:/Users/runneradmin/.nuget/packages/` and HTTP cache to
`C:/Users/runneradmin/AppData/Local/NuGet/v3-cache`. Runner-temp resolved them to `D:/a/_temp/nuget/packages` and
`D:/a/_temp/nuget/http-cache`. Fresh package consumers retained their own `.packages` directories in both modes.
The opt-in comparison remains available; no remote archive cache or SDK policy change is introduced.

To repeat after this increment merges, dispatch the existing evaluation workflow on the selected revision with
`nuget-cache-experiment=default` or `runner-temp`; `off` is the normal two-platform evaluation. For example:

```powershell
gh workflow run evaluation.yml --repo sotsera/rafter --ref main -f nuget-cache-experiment=default
gh workflow run evaluation.yml --repo sotsera/rafter --ref main -f nuget-cache-experiment=runner-temp
```

Use a single revision and record each new sample; do not relabel the observations above as checks of a later commit.

### Local regression verification

Local regression: the integration project built with zero warnings/errors, and the package-consumer class passed
**1 test, 0 failures, 0 skips** with both cache environment variables set to an ambient directory. Project and
file-app consumers used fresh private caches; the ambient directory did not receive `Sotsera.Rafter`. The first
attempt rejected Visual Studio's legitimate fallback folder; the corrected assertion checks the primary restore
path. Both TRX reports remain under `artifacts/test-results/ci-cache-package*/`.

The previous main runs were Windows 9m04s before #55 and 9m08s after it. Their job contents were identical; #55 reduced
wasted evaluations rather than accelerating an individual job. The [post-merge run](https://github.com/sotsera/rafter/actions/runs/40235060589)
passed both platforms, packaging, and redirected checks. Its Windows restore was 22s and full tests were 6m57s.

Retain raw downloaded logs, job/step metadata, and comparison data under `artifacts/evidence/ci-cache/`. CI artifacts
expire after 14 days; keep the summarized method/results here and the PR as durable records.
