# Preview releases

The first prepared version is **0.1.0-preview.1**, tracked by
[issue #72](https://github.com/sotsera/rafter/issues/72). It is a prepared artifact built from this source,
not a claim of publication. Consume it from a local feed using the [package guide](../guides/local-package.md).
The [release notes](../../CHANGELOG.md) describe its scope and limits.

## Version and compatibility policy

Use explicit prerelease versions `0.1.0-preview.N`, increasing N for each published preview. Never replace the
bytes of a published version. Local experiments may override `Version`, but consumers must use that same version
and a fresh cache when reusing it. A preview may contain intentional breaking changes; describe the affected calls,
replacement, and migration in the release notes. A preview number does not promise stable API compatibility.

The runtime tracks its public signatures, parameter names, defaults, and nullable annotations in
[PublicAPI.Unshipped.txt](../../src/Sotsera.Rafter/PublicAPI.Unshipped.txt). Additions and removals fail the build
with RS0016/RS0017 until the inventory is explicitly updated. Review the API diff alongside the source and release
notes. This detects declaration drift, not behavioral compatibility, lifecycle changes, or every binary change;
consumer and regression tests remain necessary. The inventory is not an API design approval by itself.

No API is marked shipped before publication. On release, move the approved entries into
[PublicAPI.Shipped.txt](../../src/Sotsera.Rafter/PublicAPI.Shipped.txt) and retain the nullable header in both files.
Later additions belong in Unshipped; intentional removals of shipped entries use the analyzer's `*REMOVED*` format
and require migration notes. Do not silently regenerate a shipped baseline to hide a breaking change.

The build-only Microsoft.CodeAnalysis.PublicApiAnalyzers dependency uses stable 3.3.4, compatible with the existing
compiler baseline, without upgrading the compiler or the shipping analyzer's Roslyn dependency. It has
`PrivateAssets="all"` and is not a runtime or consumer dependency. The
[upstream analyzer guide](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
describes inventory updates and nullable annotations.

SDK package validation runs during pack. There is no previously published baseline for this new implementation,
so this first preview does not claim cross-version binary validation. After publication, select the released
package as `PackageValidationBaselineVersion` for subsequent previews and verify restore from its release feed.
See [SDK package validation](https://learn.microsoft.com/dotnet/fundamentals/package-validation/overview).

## Package and documentation gates

The package includes the net10.0 runtime and adjacent XML documentation, the authoring analyzer, namespace import,
and README. Missing public XML summaries fail the runtime build with CS1591. The package-consumer test checks
the archive, dependency boundaries, and representative documentation members in both fresh restored caches.
It executes a regular project and a real file app outside the checkout, exercising authoring diagnostics and runtime
behavior. These XML lookup checks do not claim a new live-editor hover session.

Two isolated negative builds add and remove a public member and must fail with RS0016 and RS0017 respectively.
The three existing CI jobs run the applicable full suites; build/pack gates run in the normal pipeline.

## Release sequence

1. Review and merge the preparation PR after all three required checks pass. Verify its merged-main evaluation.
2. Review release notes, version, platform limits, and the exact package produced from that merged commit.
3. Verify fresh file-app/project consumers against those exact package bytes; retain their reports and package hash.
4. Publish only as an explicit release action to the selected feed, then create the matching tag and release record
   with the source commit, package hash, notes, and verification links. Do not publish from an uncommitted checkout.
5. Record the shipped API baseline and select it for subsequent compatibility checks.

The current work prepares and validates the package; it neither creates a release/tag nor publishes to NuGet.org.
Windows process ownership is the supported native baseline. Portable Linux tests cover commands, inputs, targets,
and portable consumers. Linux-native ownership needs a separate milestone; macOS and native AOT remain unverified.

## Local preparation verification — 2026-10-29

On Windows x64, the Release solution build passed with **0 warnings and 0 errors**. Enabling documentation initially
reported 36 missing public member summaries; those summaries were added, with no runtime behavior changes.
The integration project was rebuilt after adding the two negative API checks. The focused command was:

```powershell
dotnet test --project tests/Rafter.IntegrationTests/Rafter.IntegrationTests.csproj --configuration Release --no-build -p:IncludeLongRunningTests=true --filter-class Rafter.IntegrationTests.Consumers.PackageConsumerTests --minimum-expected-tests 3 --report-trx --results-directory artifacts/test-results/preview-readiness-package-retry
```

**Passed: 3, Failed: 0, Skipped: 0.** This includes fresh project/file-app package consumers, package XML and dependency
checks, and explicit RS0016/RS0017 failures from the two deliberately changed isolated API copies. The package tests
exercise the consumer features and four authoring diagnostics already covered by that class. Local pack with
`EnablePackageValidation` also passed. This does not claim a full local regression or new live editor/terminal,
Linux-native, macOS, or AOT verification; full hosted checks remain a PR delivery gate.

Logs, the initial failed test invocation, and protection-setting evidence are retained in
`artifacts/evidence/preview-readiness/`; the successful TRX is under the results directory named above.
The first test invocation failed during the runner handshake and ran zero tests; separating the build and using
the repository's standard no-build invocation produced the passing result. The failed attempt is not test coverage.
The local package is under `artifacts/packages/preview-readiness/`; it is a preparation artifact from an uncommitted
working tree, not the immutable package to publish. Final release bytes must come from the reviewed merged revision.
