# Packaging

Stipulate ships as one NuGet package, `Stipulate`, packed by `Stipulate.Package` from the other
projects' outputs:

| In the package | What | Used by |
|---|---|---|
| `lib/netstandard2.0/` | `Stipulate.Contracts`, for `[ComparableValidation]` | the consumer's code |
| `analyzers/dotnet/cs/` | the analyzer, the code fix, and the generator | the compiler and the IDE |
| `tools/net8.0/` | the weaver and Mono.Cecil | the build, through `dotnet` |
| `buildTransitive/Stipulate.targets` | the build integration: the mode, the weaving, the generator's removal outside Interceptors mode, `StipulateTransitive`, `StipulateFSharpChecks` | MSBuild |
| `buildTransitive/Stipulate.ArgumentChecks.cs`, `.vb`, `.fs` | the run-time check that woven methods and interceptors call, compiled into each project Stipulate enforces, in its language | the compiler, through `Stipulate.targets` |

The package has no dependencies. Package IDs keep the short name; the assemblies inside are
`Net.NowhereAtAll.Stipulate.*`, as everywhere else.

## One build integration, used twice

`buildTransitive/Stipulate.targets` is the only copy of the build integration. This repository's
own projects (`ArrayFoo`, and the consumer projects the integration tests build) import it
through `Directory.Build.targets`, with project references in place of the package's files and
the repository's own build of the weaver. So every build here exercises what ships, and the
package tests then check the packed copy: `Stipulate.Integration.Test`'s `PackageTest` packs the
package once per run and builds projects that reference it, in each mode, for net10.0,
netstandard2.0 and net472, and programs that get it only through a library, by a project
reference and by a package reference.

`buildTransitive/`, not `build/`: a project that gets Stipulate only through another project or
package must be held to the contracts it implements, and woven, like any other. NuGet hands
`build/` only to the projects that reference the package themselves, but the analyzers to every
project, so with `build/` the others got the generator without what makes it compile, and failed
with CS9137. `StipulateTransitive=false` leaves the others out (see
[enforcement modes](../use/enforcement-modes.md#projects-that-get-stipulate-through-another)).

## Versions

`Directory.Build.props` sets `<Version>0.0.0-dev</Version>`, the version of every local build.
A release's version is the newest `## [x.y.z]` heading in `CHANGELOG.md`; it is never edited into
a project file.

## Releasing

1. In a pull request, move the `## [Unreleased]` notes under a new heading, for example
   `## [0.1.0-preview.1] - 2026-10-25`, and leave an empty `## [Unreleased]` above it.
2. Merge it to `main`. `.github/workflows/publish.yml` reads the heading, stamps the version into
   `Directory.Build.props`, builds and tests in Release (the package tests included, at that
   version), packs, pushes to nuget.org, tags the commit `v0.1.0-preview.1`, and opens a GitHub
   Release with that section as its notes.
3. Once nuget.org lists it, some minutes later, check it as a consumer gets it:

   ```shell
   python3 scripts/smoke-test-release.py
   ```

   For the newest release in `CHANGELOG.md` (or the version given), it builds a C#, a Visual
   Basic and an F# program, in Release, outside the repository and from nuget.org alone, and
   requires each to print the message the woven check throws.

A merge that adds no new heading publishes nothing. A version with a `-suffix` is published as a
prerelease. A release whose push half-failed can be pushed again with the workflow's
`republish_version` input.

## One-time setup on nuget.org

The workflow publishes with nuget.org's trusted publishing: GitHub proves to nuget.org which
repository and workflow is asking, and gets a short-lived key, so no API key is stored anywhere.

1. On nuget.org, under your account's **Trusted Publishing**, add a policy for the repository
   owner `nilvon9wo`, repository `Stipulate`, and workflow file `publish.yml`.
2. In the GitHub repository's **Settings > Secrets and variables > Actions > Variables**, add
   `NUGET_USERNAME`: your nuget.org profile name (not your email address).

The first push creates the package ID `Stipulate` on nuget.org, owned by that account.
