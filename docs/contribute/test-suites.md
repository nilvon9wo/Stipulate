# Test Suites

One test project per package, each measuring that package at 100% (see
[coverage-standards](coverage-standards.md)), plus one that builds a consumer project:

| Project | Proves |
|---|---|
| `ParameterValidation.Analyzers.Test` | The analyzer, over real compilations: contracts, guarantees, `[Range]` limits, custom attributes and their descriptions, messages, incomplete code; and the documentation examples. |
| `ParameterValidation.CodeFixes.Test` | The restate code fix, one at a time and fix-all, in an in-memory workspace. |
| `ParameterValidation.Contracts.Test` | `[ComparableValidation]` records what it is given. |
| `ParameterValidation.Generator.Test` | The generator, through Roslyn's driver with interceptors enabled: what an intercepted call does, every kind of attribute constant, which calls are intercepted, and that unrelated edits leave the pipeline cached. |
| `ParameterValidation.Weaver.Test` | The weaver, on assemblies compiled to disk, woven, then loaded and called: what a woven method does, every kind of attribute constant, symbols, re-weaving, the command's exit codes and messages. |
| `ParameterValidation.Integration.Test` | A real `dotnet build` of a consumer project in each mode, and the build errors PV0004 and PV0005. Not measured. |
| `ArrayFooTest` | The sample that started it all, woven by the real build. |

`ParameterValidation.Testing` holds what the analyzer, code fix and contracts tests share:
compiling C# in memory, diagnostic markup, running the analyzer.

```shell
python3 scripts/run-tests.py --solution ParameterValidation.slnx                  # everything
dotnet test --project ParameterValidation.Weaver.Test                              # one package, with its coverage gate
```

---

## Documentation examples

`DocumentationExampleTest`, in `ParameterValidation.Analyzers.Test`, reads every ` ```csharp `
block in `README.md` and `docs/`, compiles it, runs the analyzer over it, and requires exactly
the diagnostics its comments show: a comment `// error PV1001: ...` or `// warning PV1002` for
each one, and no compilation errors. A page that claims what the analyzer doesn't do fails the
build, and so does an example that no longer compiles.

Two markers, on the line just before a fence, change how it is read:

- `<!-- sketch -->`: illustrative, not compiled. Use it only for code that can't stand alone,
  such as a "no / yes" formatting comparison.
- `<!-- reference: Name -->`: compiled as an assembly named `Name`, from outside the solution,
  and referenced by the examples that follow it on the same page. That is how a page shows an
  attribute from a package (see [PV2002](../rules/PV2002.md)).

Every example is compiled with the usings `System.ComponentModel.DataAnnotations` and
`Net.NowhereAtAll.ParameterValidation.Contracts`, so pages needn't repeat them.

`scripts/verify-doc-links.py` checks that every relative link in `README.md` and `docs/`, and
every `#anchor`, resolves.

---

## Keep each project single-purpose

The integration tests are slow (each one restores and builds a project) and run one at a time
(`xunit.runner.json`): keep them for what only a real build can show, and test everything else
in the package's own project.
