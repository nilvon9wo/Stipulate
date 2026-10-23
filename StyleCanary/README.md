# Style canary

Deliberately non-compliant code. **Do not fix it.** It is not part of
`ArrayFoo.slnx`, and `scripts/check-line-layout.py` skips it, so no ordinary gate
ever reports it.

Every violation in `Violations.cs` carries an `// expect: <gate>:<rule>` marker
naming the gate that must report it on that line. `scripts/verify-gates.py`
runs each gate against this project and fails if any expected report is
missing, or if a gate has no marker exercising it. A gate that silently stops
running looks exactly like one that passes; this is what tells them apart.

| Gate | Command `verify-gates.py` runs |
|---|---|
| `build` | `dotnet build StyleCanary/StyleCanary.csproj --no-incremental` (errors only: the compiler warning `CS0649` must surface as an error, proving `TreatWarningsAsErrors`) |
| `format` | `dotnet format StyleCanary/StyleCanary.csproj --verify-no-changes --severity info` |
| `layout` | `python3 scripts/check-line-layout.py StyleCanary/Violations.cs` |
| `inspect` | `python3 scripts/inspect-code.py StyleCanary/StyleCanary.csproj` |

The gates that cannot be canaried with a static file are proven by a temporary
change to the real repository, always undone afterwards:

- **coverage**: an uncovered class is added to `ParameterValidation.Contracts/`;
  `ParameterValidation.Contracts.Test` must then fail its 100% threshold, with
  every test passing;
- **INCONCLUSIVE test runs**: one test module's apphost is hidden so it cannot
  start; `scripts/run-tests.py` must then report `INCONCLUSIVE` instead of
  `dotnet test`'s "Passed!".

When you add a rule to `.editorconfig` or a new gate to CI, add a violation for
it here.
