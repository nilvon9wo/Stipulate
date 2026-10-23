#!/usr/bin/env python3
"""
Prove every quality gate still catches what it is supposed to catch.

A gate that silently stops running looks exactly like a gate that passes. So:

* StyleCanary/ holds deliberately non-compliant code, each violation tagged
  `// expect: <gate>:<rule>` on the line a gate must report. Each static gate
  (build, format, layout, inspect) runs against it; every marker must be
  matched by a finding, and every gate must have at least one marker.
* run-tests.py's INCONCLUSIVE verdict is proven by hiding one test module's
  apphost, so it cannot start - the way Smart App Control blocks one on
  Windows - and requiring that verdict instead of dotnet test's "Passed!".

Every temporary change is undone again, however the run ends. Run after
`dotnet tool restore` (inspectcode). See StyleCanary/README.md.

Usage: verify-gates.py
Exits 1, naming every gate that missed something, if any gate is unproven.
"""
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CANARY_PROJECT = os.path.join("StyleCanary", "StyleCanary.csproj")
CANARY_SOURCE = os.path.join("StyleCanary", "Violations.cs")

EXPECT_MARKER = re.compile(r"// expect: (?P<gate>[\w-]+):(?P<rule>[\w.-]+)")
# `File.cs(line,col): error RULE` - MSBuild's diagnostic shape, which both
# `dotnet build` and `dotnet format` use. `info` counts: format fails on it.
MSBUILD_DIAGNOSTIC = re.compile(
    r"(?P<path>[^\s(:]+\.cs)\((?P<line>\d+),\d+\): (?:error|warning|info) (?P<rule>[A-Za-z]+\d*)"
)
# The build gate only reports what fails the build: errors. A compiler
# warning the canary expects (CS0649) therefore proves TreatWarningsAsErrors.
BUILD_ERROR = re.compile(r"(?P<path>[^\s(:]+\.cs)\((?P<line>\d+),\d+\): error (?P<rule>[A-Za-z]+\d*)")
# `path:line: rule: message` - the shape check-line-layout.py and
# inspect-code.py print.
COLON_DIAGNOSTIC = re.compile(r"^(?P<path>[^\s:]+\.cs):(?P<line>\d+): (?P<rule>[\w.-]+):", re.M)

UNSTARTABLE_PROJECT = os.path.join("ParameterValidation.CodeFixes.Test", "ParameterValidation.CodeFixes.Test.csproj")
UNSTARTABLE_APPHOST = os.path.join(
    "ParameterValidation.CodeFixes.Test", "bin", "Debug", "net10.0",
    "ParameterValidation.CodeFixes.Test" + (".exe" if os.name == "nt" else ""),
)


def run(command: list) -> tuple:
    completed = subprocess.run(
        command, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace"
    )
    return completed.returncode, completed.stdout + completed.stderr


def python_script(name: str, *arguments: str) -> list:
    return [sys.executable, os.path.join("scripts", name), *arguments]


def findings(pattern: re.Pattern, output: str) -> set:
    return {
        (os.path.basename(match["path"]), int(match["line"]), match["rule"])
        for match in pattern.finditer(output)
    }


STATIC_GATES = {
    "build": lambda: findings(
        BUILD_ERROR, run(["dotnet", "build", CANARY_PROJECT, "-nologo", "--no-incremental"])[1]
    ),
    "format": lambda: findings(
        MSBUILD_DIAGNOSTIC,
        run(["dotnet", "format", CANARY_PROJECT, "--verify-no-changes", "--severity", "info"])[1],
    ),
    "layout": lambda: findings(COLON_DIAGNOSTIC, run(python_script("check-line-layout.py", CANARY_SOURCE))[1]),
    "inspect": lambda: findings(COLON_DIAGNOSTIC, run(python_script("inspect-code.py", CANARY_PROJECT))[1]),
}


def expectations() -> list:
    with open(os.path.join(ROOT, CANARY_SOURCE), encoding="utf-8-sig") as fh:
        lines = fh.read().splitlines()
    return [
        (match["gate"], os.path.basename(CANARY_SOURCE), number, match["rule"])
        for number, line in enumerate(lines, start=1)
        for match in EXPECT_MARKER.finditer(line)
    ]


def verify_static_gates() -> bool:
    expected = expectations()
    reported = {gate: run_gate() for gate, run_gate in STATIC_GATES.items()}
    missed = [
        (gate, file_name, line, rule)
        for gate, file_name, line, rule in expected
        if (file_name, line, rule) not in reported.get(gate, set())
    ]
    unproven = sorted(set(STATIC_GATES) - {gate for gate, *_ in expected})
    for gate, file_name, line, rule in missed:
        print(f"[MISSED] {gate} did not report {rule} at {file_name}:{line}")
    for gate in unproven:
        print(f"[UNPROVEN] no StyleCanary marker exercises the '{gate}' gate")
    print(f"static gates: {len(expected) - len(missed)}/{len(expected)} expected findings reported")
    return not missed and not unproven


def runner_reports_an_unstarted_module() -> bool:
    """With one module unable to start, run-tests.py must say INCONCLUSIVE, not pass."""
    build_code, _ = run(["dotnet", "build", UNSTARTABLE_PROJECT, "-nologo"])
    apphost = os.path.join(ROOT, UNSTARTABLE_APPHOST)
    hidden = apphost + ".gate-canary"
    if build_code != 0 or not os.path.exists(apphost):
        print(f"run-tests.py could not be checked: no apphost at {UNSTARTABLE_APPHOST}")
        return False
    os.rename(apphost, hidden)
    try:
        code, output = run(python_script("run-tests.py", "--project", UNSTARTABLE_PROJECT, "--no-build"))
    finally:
        os.rename(hidden, apphost)
    caught = code == 3 and "INCONCLUSIVE" in output
    print(f"run-tests.py reported a module that never started as INCONCLUSIVE: {caught}")
    return caught


def main() -> int:
    results = {
        "static gates": verify_static_gates(),
        "inconclusive runs": runner_reports_an_unstarted_module(),
    }
    failed = [name for name, proven in results.items() if not proven]
    if failed:
        print(f"verify-gates: NOT PROVEN - {', '.join(failed)}", file=sys.stderr)
        return 1
    print("verify-gates: every gate caught its canary")
    return 0


if __name__ == "__main__":
    sys.exit(main())
