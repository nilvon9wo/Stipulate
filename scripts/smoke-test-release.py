#!/usr/bin/env python3
"""
A published release of Stipulate, as a consumer gets it from nuget.org, must
weave a C#, a Visual Basic and an F# project.

For each language, in a fresh folder outside the repository (so that neither
Directory.Build.props nor a local feed is involved), it creates a program that
references the package from nuget.org alone, builds it in Release (so that F#'s
optimizer is on), and calls a method whose parameter is [Range(1, 10)] with 0:
the program must print the message the woven check throws, word for word.

Usage:
  python3 scripts/smoke-test-release.py [version]

The version defaults to the newest release heading in CHANGELOG.md. nuget.org
lists a package some minutes after publish.yml pushes it: until it does, this
exits non-zero, saying so. Exit non-zero (listing every failure) unless every
language's program prints its expected message.
"""
import json
import os
import re
import subprocess
import sys
import tempfile
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CHANGELOG = os.path.join(ROOT, "CHANGELOG.md")
HEADING_RE = re.compile(r"^\s*##\s*\[([^\]]+)\]")
VERSIONS_URL = "https://api.nuget.org/v3-flatcontainer/stipulate/index.json"

NUGET_CONFIG = """<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"""

PROJECT = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Stipulate" Version="{version}" />
  </ItemGroup>
  <ItemGroup>{compile}</ItemGroup>
</Project>
"""

# Each language: project extension, source file, the program, the Compile items
# F# needs (it compiles only the files it is given), and the message expected.
MESSAGE = (
    "{method} rejected argument 'level' by [Range]: The field level must be"
    " between 1 and 10. The argument was 0. (Parameter 'level')"
)

CSHARP = """using System;
using System.ComponentModel.DataAnnotations;

// args.Length, 0, rather than the constant, which the analyzer rejects at compile time (ST3001).
try { Levels.Check(args.Length); Console.WriteLine("not validated"); }
catch (ArgumentException e) { Console.WriteLine(e.Message); }

public static class Levels
{
    public static void Check([Range(1, 10)] int level) { }
}
"""

VISUAL_BASIC = """Imports System
Imports System.ComponentModel.DataAnnotations

Public Module Levels
    Public Sub Check(<Range(1, 10)> level As Integer)
    End Sub
End Module

Module Program
    Sub Main()
        Try
            Levels.Check(0)
            Console.WriteLine("not validated")
        Catch e As ArgumentException
            Console.WriteLine(e.Message)
        End Try
    End Sub
End Module
"""

FSHARP = """open System
open System.ComponentModel.DataAnnotations

type Levels =
    static member Check([<Range(1, 10)>] level: int) = ()

try
    Levels.Check 0
    Console.WriteLine "not validated"
with :? ArgumentException as e ->
    Console.WriteLine e.Message
"""

LANGUAGES = [
    ("C#", "csproj", "Program.cs", CSHARP, "", "Levels.Check(int)"),
    ("Visual Basic", "vbproj", "Program.vb", VISUAL_BASIC, "", "Levels.Check(int)"),
    # F# puts a type declared at the top of Program.fs in an implicit module, Program.
    ("F#", "fsproj", "Program.fs", FSHARP, '<Compile Include="Program.fs" />', "Program.Levels.Check(int)"),
]


def newest_release() -> str:
    with open(CHANGELOG, encoding="utf-8") as f:
        for line in f:
            match = HEADING_RE.match(line)
            if match and match.group(1).strip() != "Unreleased":
                return match.group(1).strip()
    sys.exit("CHANGELOG.md has no release heading, so there is no release to test: pass a version.")


def listed_versions() -> list[str]:
    with urllib.request.urlopen(VERSIONS_URL) as response:
        return json.load(response)["versions"]


def smoke_test(folder: str, version: str, language: tuple) -> str | None:
    name, extension, source, program, compile_items, method = language
    project_dir = os.path.join(folder, extension)
    os.makedirs(project_dir)
    with open(os.path.join(project_dir, "Smoke." + extension), "w", encoding="utf-8") as f:
        f.write(PROJECT.format(version=version, compile=compile_items))
    with open(os.path.join(project_dir, source), "w", encoding="utf-8") as f:
        f.write(program)

    build = subprocess.run(
        ["dotnet", "build", project_dir, "--configuration", "Release"],
        capture_output=True, text=True,
    )
    if build.returncode != 0:
        errors = sorted({line.strip() for line in build.stdout.splitlines() if " error " in line})
        return f"{name}: the build failed:\n    " + "\n    ".join(errors or [build.stdout[-2000:]])

    run = subprocess.run(
        ["dotnet", os.path.join(project_dir, "bin", "Release", "net10.0", "Smoke.dll")],
        capture_output=True, text=True,
    )
    expected = MESSAGE.format(method=method)
    actual = run.stdout.strip()
    if actual != expected:
        return f"{name}: expected\n    {expected}\nbut the program printed\n    {actual or run.stderr.strip()}"
    print(f"{name}: {actual}")
    return None


def main() -> int:
    version = sys.argv[1] if len(sys.argv) > 1 else newest_release()
    if version not in listed_versions():
        print(f"nuget.org doesn't list Stipulate {version} yet; it can take some minutes after publishing.")
        return 1

    with tempfile.TemporaryDirectory(prefix=f"stipulate-smoke-{version}-") as folder:
        with open(os.path.join(folder, "nuget.config"), "w", encoding="utf-8") as f:
            f.write(NUGET_CONFIG)
        failures = [failure for language in LANGUAGES if (failure := smoke_test(folder, version, language))]

    for failure in failures:
        print(failure, file=sys.stderr)
    if failures:
        return 1
    print(f"Stipulate {version} from nuget.org weaves C#, Visual Basic and F#.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
