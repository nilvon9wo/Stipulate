#!/usr/bin/env python3
"""
`dotnet test`, but a test module that never ran is reported as INCONCLUSIVE.

When a test module cannot start at all - Windows Smart App Control blocking
a freshly built test host (0x800711C7), a missing or non-executable apphost -
`dotnet test` (Microsoft Testing Platform) still prints
"Test run summary: Passed!" and "failed: 0", counting only the modules that
did run. Its exit code is non-zero, but the summary a human reads says the
opposite. A module that ran zero tests reads the same way.

This passes every argument straight through to `dotnet test`, prints its
output as it arrives, and then - if any module never started or ran no
tests - ends with an unmistakable verdict naming them:

    Test run summary: INCONCLUSIVE - 1 test module(s) did not run: ...

and exits with INCONCLUSIVE_EXIT_CODE, never 0. Otherwise it exits with
`dotnet test`'s own code.

Usage: run-tests.py [any dotnet test arguments]
    run-tests.py --solution ParameterValidation.slnx
"""
import re
import subprocess
import sys

INCONCLUSIVE_EXIT_CODE = 3
# "The following exception occurred when running the test module with
# RunCommand '<path>' ..." - the module's process never started.
NEVER_STARTED = re.compile(r"exception occurred when running the test module with RunCommand '([^']+)'")
# "<path>.dll (net10.0) Zero tests ran" - started, but nothing executed.
RAN_NOTHING = re.compile(r"^\s*(\S+\.dll)\s.*Zero tests ran", re.M)


def module_name(path: str) -> str:
    return re.split(r"[\\/]", path)[-1]


def main(arguments: list) -> int:
    process = subprocess.Popen(
        ["dotnet", "test", *arguments],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    assert process.stdout is not None  # stdout=PIPE above
    captured = []
    for line in process.stdout:
        sys.stdout.write(line)
        captured.append(line)
    exit_code = process.wait()
    output = "".join(captured)

    did_not_run = sorted(
        {module_name(path) for path in NEVER_STARTED.findall(output)}
        | {module_name(path) for path in RAN_NOTHING.findall(output)}
    )
    if did_not_run:
        print(
            f"\nTest run summary: INCONCLUSIVE - {len(did_not_run)} test module(s) did not run: "
            + ", ".join(did_not_run)
            + "\n  Their tests were not executed, so any 'Passed!' above covers only the modules that ran."
            + "\n  On Windows, check the CodeIntegrity event log for a Smart App Control block"
            + " (docs/contribute/local-development.md).",
            file=sys.stderr,
        )
        return INCONCLUSIVE_EXIT_CODE
    return exit_code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
