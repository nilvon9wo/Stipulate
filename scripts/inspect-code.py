#!/usr/bin/env python3
"""
ReSharper inspections as a CI gate: what Roslyn misses.

`dotnet build` never fails on IDE0005 (unnecessary using), even with
EnforceCodeStyleInBuild; ReSharper's free command-line `inspectcode` reports
it (RedundantUsingDirective), along with redundant casts, qualifiers and
nullable suppressions, unresolvable doc-comment references, and more. Every
finding fails this gate - inspections that contradict this repo's documented
standards are switched off, with reasons, in `.editorconfig`, never left to
pile up as noise.

Three details matter, each of which once produced a silent pass or a bogus
failure:

* `--no-build`: without it inspectcode builds first and, if the build fails,
  writes NO report at all - which reads exactly like "no findings". The build
  gate owns compilation; this gate only inspects. A missing report is treated
  as a failure here, never as a pass.
* `--caches-home` pointing at a fresh, throwaway directory: inspectcode's
  default cache can keep serving `.editorconfig` settings from before an
  edit, reporting findings the current settings no longer produce (or
  missing ones they now do).
* `--severity=SUGGESTION`: the default (WARNING) hides most of the useful
  inspections, RedundantUsingDirective included.

Usage:
    inspect-code.py                          # the whole solution
    inspect-code.py <project-or-solution>    # e.g. the canary project

Prints `path:line: RuleId: message` per finding; exits 1 on any finding, or
if inspectcode wrote no report.
"""
import json
import os
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_TARGET = os.path.join(ROOT, "ParameterValidation.slnx")


def run_inspectcode(target: str, workdir: str) -> str:
    report = os.path.join(workdir, "inspect.sarif")
    command = [
        "dotnet", "jb", "inspectcode", target,
        "--no-build",
        "--severity=SUGGESTION",
        f"--caches-home={os.path.join(workdir, 'caches')}",
        f"-o={report}",
    ]
    completed = subprocess.run(
        command, cwd=ROOT, capture_output=True, text=True, encoding="utf-8",
        errors="replace",
    )
    if completed.returncode != 0 or not os.path.exists(report):
        print(completed.stdout)
        print(completed.stderr, file=sys.stderr)
        print(
            f"inspectcode exited {completed.returncode} and wrote "
            f"{'a' if os.path.exists(report) else 'NO'} report - treating as "
            "a failure, not as 'no findings'.",
            file=sys.stderr,
        )
        return ""
    return report


def findings_in(report: str) -> list:
    with open(report, encoding="utf-8-sig") as fh:
        sarif = json.load(fh)
    findings = []
    for result in sarif["runs"][0]["results"]:
        location = result["locations"][0]["physicalLocation"]
        uri = location["artifactLocation"]["uri"]
        line = location["region"]["startLine"]
        message = result["message"]["text"].split("\n")[0]
        findings.append((uri, line, result["ruleId"], message))
    return sorted(findings)


def main(arguments: list) -> int:
    target = arguments[0] if arguments else DEFAULT_TARGET
    with tempfile.TemporaryDirectory() as workdir:
        report = run_inspectcode(target, workdir)
        if not report:
            return 1
        findings = findings_in(report)
    for uri, line, rule, message in findings:
        print(f"{uri}:{line}: {rule}: {message}")
    if findings:
        print(f"inspectcode: {len(findings)} finding(s)", file=sys.stderr)
        return 1
    print("inspectcode: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
