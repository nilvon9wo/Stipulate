# Coding Standards

The rules this repository's code is held to. They apply to anyone changing it, human or AI.
When a change is reviewed, this is the checklist.

**The authoritative style rules:**

- **`.editorconfig`**, at the repository root, ported from XFTY. Every rule in it is
  `severity = error` on purpose; see [Everything is `error`](#everything-is-error).
  `EnforceCodeStyleInBuild=true`, so `dotnet build` fails on almost all of them, IDE1006 naming
  included. What it misses (unnecessary usings, line length, wrapped-`)` placement) has its own
  gate; see [Quality gates](#quality-gates). One deviation from XFTY: files must end with a
  newline.
- **The house rules**, layered on top of the analyzers (not tool-enforced; reviewed by hand):
  1. Prefer functional over imperative. A `foreach` is acceptable where the alternative is a
     contorted LINQ chain; a bare `for`/`while` counter almost never is.
  2. Prefer explicit over implicit.
  3. Keep methods short: a method past ~10 lines is usually doing two things. A smell to
     investigate, not a hard limit to contort code around.
  4. Never more than one expression per line.
  5. Blocks must not be nested more than two deep.
  6. Never nest expressions; give the inner one a name.
  7. Use variables or methods to name the results of all complex expressions.
  8. Name classes, methods and variables to communicate intentions; never a single letter or an
     abbreviation.
  9. Nouns name objects; verbs name methods.
  10. Adjectives name interfaces, prefixed with `I`: an interface says what its implementers are
      able to do (`IConstraintComparing`).
  11. Name booleans `isSomething`, `hasSomething`, `wasSomething`.
  12. Always use `this`, except for static members.
  13. Extract magic numbers and strings to constants.
  14. Prefer a ternary to if/else, on three lines: the condition, then each outcome.
  15. Don't nest a complicated expression inside a ternary: extract it first.
  16. Intention and separation of concerns over line count.
  17. Never use nested classes: a helper scoped to one file is a `file sealed class` at namespace
      scope.
  18. Avoid circular dependencies.

---

## Everything is `error`

Every diagnostic in `.editorconfig` is `severity = error`, deliberately. A diagnostic is fixed,
never demoted to `warning` or `suggestion` to sit on a list that then gets ignored. The
compiler's own warnings and NuGet's are no exception: `TreatWarningsAsErrors` is on for every
project (`Directory.Build.props`).

If a rule genuinely can't be satisfied at one site, restructure the code so that it is, rather
than suppress it. A `#pragma` or `[SuppressMessage]` is a last resort, carries a comment saying
why, and is called out in the commit message.

---

## Naming

| Symbol | Style |
|---|---|
| types, namespaces, methods, non-private properties, type parameters | `PascalCase` |
| interfaces | `IPascalCase` |
| `const` and `static readonly` | `PascalCase` |
| private instance fields, including primary-constructor parameters held in fields | `_camelCase` |
| mutable private static fields | `s_camelCase` |
| parameters, locals, lambda parameters | `camelCase` |

Every assembly and namespace starts with `Net.NowhereAtAll.`, as XFTY's do;
`Directory.Build.props` sets it from the project name, so the folders and projects keep the
short name (`Stipulate.Weaver` builds `Net.NowhereAtAll.Stipulate.Weaver.dll`).

A primary-constructor parameter that a class keeps is held in a `private readonly` field, not
captured: the field names the state.

---

## Formatting

- **Line length: 120, hard.** Roslyn reads `max_line_length` but never reports on it, so
  `scripts/check-line-layout.py` does.
- **One expression per line; one declaration per line.**
- **A wrapped call or declaration closes with `)` on its own line**, aligned with the statement
  that opened it. A trailing lambda block or collection expression doesn't count as wrapping.

<!-- sketch -->
```csharp
// no
return new CallSite(
    methodId, signature, forwardingCall, returnsVoid, checks, location);

// yes
return new CallSite(
    methodId,
    signature,
    forwardingCall,
    returnsVoid,
    checks,
    location
);
```

---

## Quality gates

CI (`.github/workflows/ci.yml`) fails the build on any of:

| Gate | Covers |
|---|---|
| `dotnet build` (`EnforceCodeStyleInBuild`, `TreatWarningsAsErrors`) | compilation, with every warning an error, and every `.editorconfig` rule that runs in the build |
| `dotnet format Stipulate.slnx --verify-no-changes --severity info` | whitespace and formatting, and a second pass over the style rules |
| `scripts/inspect-code.py` (ReSharper `inspectcode`) | what Roslyn misses, such as unnecessary usings and unresolvable doc-comment references; every finding fails |
| `scripts/check-line-layout.py` | the 120-character ceiling and wrapped-`)` placement |
| `scripts/run-tests.py`, in Debug and Release | every test, with **100% line and branch coverage** of each package; a module that never ran makes the run `INCONCLUSIVE`, not "Passed!" |
| the documentation examples (in `Stipulate.Analyzers.Test`) and `scripts/verify-doc-links.py` | every C# example in the docs does what its comments say; every relative link resolves |
| `scripts/verify-gates.py` | every gate above still fires; see [ci](ci.md#proving-the-gates-fire) |

Run the same checks locally before pushing; see [local-development](local-development.md).

---

## Design

- **Polymorphism over branching.** A type check made in more than one place is a missing type:
  one implementation per case (`IConstraintComparing`, one per kind of constraint).
- **Immutability.** Records and immutable collections; derive rather than mutate.
- **Remove dead code** rather than working around it, or covering it. A branch whose input the
  type system or Roslyn rules out becomes a cast or a `!`, with a comment stating the invariant.
- **The analyzer runs on code that doesn't compile yet.** In the IDE it sees half-typed code on
  every keystroke: it must neither throw nor report something that isn't so. Those branches stay,
  and are tested (`PreconditionAnalyzerIncompleteCodeTest`).
- **A consumer must never have to debug the tools.** A failure names its cause: the weaver
  reports MSBuild errors with codes and messages that say what to do, and the analyzer's
  messages quote the attributes on both sides.

---

## Testing

- **100% line and branch coverage**, enforced on every `dotnet test`; see
  [coverage-standards](coverage-standards.md).
- **One test class per unit under test**, or per behaviour of a large one
  (`PreconditionAnalyzerGuaranteeTest`, `PreconditionAnalyzerRangeLimitTest`).
- **One behaviour per test method.** A positive and a negative case are two behaviours; a
  `[Theory]` covers data rows of one behaviour.
- **The Act is exactly one statement.** Arrange declares, Act assigns, Assert reads.
- **AAA comments, verbatim**: `// Arrange`, `// Act`, `// Assert`; an Arrange with nothing to do
  says why (`// Arrange - nothing beyond the arguments`).
- **Names:** `<MethodUnderTest>_When<Condition>_<ExpectedOutcome>`, with no `Test` prefix:
  `Weave_WhenTheAssemblyIsAlreadyWoven_ReturnsNull`.
- **`Assert.Throws<TheSpecificException>`**, never a bare `Exception`, and never a try/catch.
- **Test against the real thing.** The analyzer runs over real compilations, the generator
  through Roslyn's driver, the weaver over assemblies on disk that are then loaded and called,
  and the build integration through real `dotnet build`s.
