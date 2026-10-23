# Coverage Standards

## 100% line and branch coverage, on every run

Every `ParameterValidation.*.Test` project measures the one package it tests and **fails below
100% line and 100% branch coverage**: every guard, every `switch` arm, both sides of every
ternary and null-coalescing operator. It is not a CI extra: the threshold is part of every
`dotnet test`, local or CI, wired once in `Directory.Build.props`:

- `coverlet.MTP`, not `coverlet.collector`, a VSTest data collector that silently measures
  nothing under the Microsoft Testing Platform runner that `global.json` selects.
- `--coverlet-include "[Net.NowhereAtAll.<Package>]*"`, derived from the test project's name
  (`ParameterValidation.Weaver.Test` measures `Net.NowhereAtAll.ParameterValidation.Weaver`),
  so neither the test assembly, the shared `ParameterValidation.Testing` helpers nor xunit's own
  DLLs drag the numbers down.
- An explicit `--coverlet-exclude`. Left to its default, coverlet.MTP excludes every assembly
  loaded alongside the tests, cut to its first two name segments: any
  `Net.NowhereAtAll.ParameterValidation.*` assembly becomes `[Net.NowhereAtAll.*]*`, which
  excludes the measured assembly too, and the threshold fails on "No coverable data".
- `ParameterValidation.Integration.Test` isn't measured: it builds a consumer project with
  `dotnet build`, so it measures no one assembly.

**Release counts too.** Optimized IL branches differently from Debug: a condition that one
configuration compiles away can be an uncovered branch in the other. CI runs the tests in both.

**When the bar is hard to reach, the code is usually the problem.** A branch no test can reach
is dead code: remove it rather than cover it. A branch that only half-written code in the IDE
can reach still gets a test: the analyzer sees that code on every keystroke. A property pattern
on a reference type carries a null test; if the value can't be null, a pattern over its values
(or a member of the type itself) leaves that unreachable branch out.

A run that can't reach 100% by design, such as one filtered to a single class, opts out:

```shell
dotnet test --project ParameterValidation.Weaver.Test -p:ParameterValidationCoverage=false --filter-class "*FileRetryTest"
```

## The gate is proven, not assumed

`scripts/verify-gates.py` adds a class no test covers to `ParameterValidation.Contracts` and
requires its tests to fail, with every test passing, and the coverage report to show the class
was measured. A coverlet that measures nothing also fails the threshold, but for the wrong
reason. See [ci](ci.md#proving-the-gates-fire).
