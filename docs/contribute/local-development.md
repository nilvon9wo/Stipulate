# Local Development

A standard .NET solution, `Stipulate.slnx`. Prerequisites: the .NET 10 SDK, and
Python 3 for the gate scripts.

---

## The loop

```shell
dotnet build Stipulate.slnx                                         # .editorconfig rules enforced
dotnet format Stipulate.slnx --verify-no-changes --severity info
python3 scripts/check-line-layout.py                                # 120 characters, wrapped ')'
dotnet tool restore && python3 scripts/inspect-code.py              # ReSharper inspections
python3 scripts/run-tests.py --solution Stipulate.slnx --no-build
python3 scripts/verify-doc-links.py
python3 scripts/verify-gates.py                                     # every gate still fires
```

These are the gates CI runs (see [ci](ci.md)). `dotnet format`, `check-line-layout.py` and
`inspect-code.py` are the ones most easily forgotten: the build passes without them, CI won't.
`inspect-code.py` needs a prior build.

Every test run enforces 100% coverage, which a filtered run can't reach; pass
`-p:StipulateCoverage=false` for those (see
[coverage-standards](coverage-standards.md)). The report, a `coverage.cobertura.*.xml` under the
test project's `bin/.../TestResults/`, shows which line or branch is missing.

---

## Visual Studio and the analyzer

Visual Studio loads the analyzer and the code fix once, when it opens the solution, and keeps
them: after rebuilding either, restart Visual Studio to see the change. The command-line build
always uses the fresh ones.

---

## Smart App Control (Windows)

Windows' Smart App Control blocks freshly built, unsigned DLLs by reputation, intermittently and
persistently per binary: a test host fails with `0x800711C7` ("An Application Control policy has
blocked this file"), and a build that weaves fails with ST0004, because the weaver was blocked.
Retrying doesn't reliably help.

A test module blocked before it starts is the worst case: `dotnet test` still prints "Test run
summary: Passed!" for the modules that did run. That is why `scripts/run-tests.py` exists: it
ends such a run with `Test run summary: INCONCLUSIVE - N test module(s) did not run` and exit
code 3.

Build and test from WSL instead, from a copy of the sources, so that the Linux restore doesn't
rewrite the `obj/` folders Visual Studio uses:

```shell
rsync -a --delete --exclude bin --exclude obj --exclude .vs --exclude .git --exclude TestResults \
    /mnt/e/path/to/Stipulate/ ~/stipulate/
cd ~/stipulate && python3 scripts/run-tests.py --solution Stipulate.slnx
```

`rsync -a` keeps the Windows files' modification times. If you edit a file in the copy and then
sync over it, MSBuild can see nothing newer and skip rebuilding: delete the copy before a final
run.
