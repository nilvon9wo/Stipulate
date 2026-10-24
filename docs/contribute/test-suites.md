# Test Suites

One test project per package, each measuring that package at 100% (see
[coverage-standards](coverage-standards.md)), plus one that builds a consumer project:

| Project | Proves |
|---|---|
| `Stipulate.Analyzers.Test` | The analyzer, over real compilations: contracts, guarantees, `[Range]` limits, custom attributes and their descriptions, messages, incomplete code; and the documentation examples. |
| `Stipulate.CodeFixes.Test` | The restate code fix, one at a time and fix-all, in an in-memory workspace. |
| `Stipulate.Contracts.Test` | `[ComparableValidation]` records what it is given. |
| `Stipulate.Generator.Test` | The generator, through Roslyn's driver with interceptors enabled: what an intercepted call does, every kind of attribute constant, which calls are intercepted, and that unrelated edits leave the pipeline cached. |
| `Stipulate.Weaver.Test` | The weaver, on assemblies compiled to disk, woven, then loaded and called: what a woven method does, every kind of attribute constant, symbols, re-weaving, the command's exit codes and messages. |
| `Stipulate.Integration.Test` | Real `dotnet build`s of consumer projects: wired to this repository, in each mode, across a change of mode, and the build errors ST0004 and ST0005; and referencing the packed package, in each mode, for net10.0, netstandard2.0 and net472, or only through a library (by project and by package reference, directly and through two more libraries), with `StipulateTransitive` on and off, plus the package's contents. Not measured. |
| `ArrayFooTest` | The sample that started it all, woven by the real build. |

`Stipulate.Testing` holds what the analyzer, code fix and contracts tests share:
compiling C# in memory, diagnostic markup, running the analyzer.

```shell
python3 scripts/run-tests.py --solution Stipulate.slnx                  # everything
dotnet test --project Stipulate.Weaver.Test                              # one package, with its coverage gate
```

---

## Documentation examples

`DocumentationExampleTest`, in `Stipulate.Analyzers.Test`, reads every ` ```csharp `
block in `README.md`, `docs/` and the package's README (`Stipulate.Package/README.md`, shown on
nuget.org), compiles it, runs the analyzer over it, and requires exactly
the diagnostics its comments show: a comment `// error ST1001: ...` or `// warning ST1002` for
each one, and no compilation errors. A page that claims what the analyzer doesn't do fails the
build, and so does an example that no longer compiles.

Two markers, on the line just before a fence, change how it is read:

- `<!-- sketch -->`: illustrative, not compiled. Use it only for code that can't stand alone,
  such as a "no / yes" formatting comparison.
- `<!-- reference: Name -->`: compiled as an assembly named `Name`, from outside the solution,
  and referenced by the examples that follow it on the same page. That is how a page shows an
  attribute from a package (see [ST2002](../rules/ST2002.md)).

Every example is compiled with the usings `System.ComponentModel.DataAnnotations` and
`Net.NowhereAtAll.Stipulate.Contracts`, so pages needn't repeat them.

`scripts/verify-doc-links.py` checks that every relative link in `README.md` and `docs/`, and
every `#anchor`, resolves.

---

## Keep each project single-purpose

The integration tests are slow (each one restores and builds a project) and run one at a time
(`xunit.runner.json`): keep them for what only a real build can show, and test everything else
in the package's own project.
