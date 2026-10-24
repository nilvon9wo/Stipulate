#!/usr/bin/env python3
"""
Enforce the two .editorconfig layout rules no Roslyn analyzer reports on.

1. `max_line_length = 120`. Roslyn reads the setting but never reports a
   violation of it, so `dotnet build` and `dotnet format` both pass a 300-
   character line.
2. `csharp_wrap_before_invocation_rpar` / `_declaration_rpar`: a call or
   declaration whose argument list spans lines closes with `)` on its own
   line. Only Rider/ReSharper honour these; neither `dotnet format` nor
   `jb cleanupcode` reports or fixes them.

Deliberately lexical, not syntactic: string and comment contents are blanked
out first (line numbers kept), then each `(` is paired with the `)` that
closes it. Good enough for layout; not for anything that needs to know what
the code means. `scripts/verify-gates.py` proves this check still fires
against `StyleCanary/`.

Usage:
    check-line-layout.py                # every *.cs in the repo
    check-line-layout.py <path> ...     # these files / directories

A directory is scanned for *.cs outside build output and the
deliberately non-compliant canary; a file named explicitly is always checked
(that is how verify-gates.py points this at the canary). Prints
`path:line: rule: message` per violation and exits 1 if there are any.
"""
import os
import re
import sys
from dataclasses import dataclass

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

MAX_LINE_LENGTH = 120
LINE_LENGTH_RULE = "line-length"
WRAP_RPAR_RULE = "wrap-rpar"
WRAP_RPAR_MESSAGE = (
    "wrapped call/declaration must close with ')' on its own line"
)
EXCLUDED_DIR_NAMES = frozenset(
    {"bin", "obj", ".git", ".vs", ".idea", "StyleCanary"}
)
CONTROL_KEYWORDS = frozenset(
    {
        "if", "while", "for", "foreach", "switch", "using", "lock", "catch",
        "when", "fixed", "return", "nameof", "typeof", "sizeof", "default",
        "checked", "unchecked", "is", "and", "or", "not", "in", "out", "ref",
        "await", "throw", "yield", "else", "case",
    }
)
IDENTIFIER_BEFORE_PAREN = re.compile(r"([A-Za-z_]\w*)\s*(<[^()]*>)?\s*$")
NOT_NEWLINE = re.compile(r"[^\n]")
STRING_OR_COMMENT = re.compile(
    r"//[^\n]*"                       # line comment
    r"|/\*.*?\*/"                     # block comment
    r'|""".*?"""'                     # raw string literal
    r'|\$?@\$?"(?:[^"]|"")*"'         # verbatim (optionally interpolated)
    r'|\$?"(?:\\.|[^"\\\n])*"'        # regular (optionally interpolated)
    r"|'(?:\\.|[^'\\\n])*'",          # character literal
    re.DOTALL,
)


@dataclass(frozen=True)
class Paren:
    line_number: int
    text_before: str
    is_opening: bool


@dataclass(frozen=True)
class Violation:
    path: str
    line_number: int
    rule: str
    message: str

    def describe(self) -> str:
        return f"{self.path}:{self.line_number}: {self.rule}: {self.message}"


def blank_strings_and_comments(text: str) -> str:
    return STRING_OR_COMMENT.sub(
        lambda match: NOT_NEWLINE.sub(" ", match.group()),
        text,
    )


def match_parens(code: str) -> list:
    """Every (opening, closing, is_wrapped) triple, in the order they close.

    `is_wrapped` is true when the parenthesised list itself breaks across
    lines - a newline directly inside the parentheses, not merely inside a
    nested `{ }` / `[ ]` / `( )`. `Foo(x => { ... });` and
    `Foo(a, [ ... ]);` keep their arguments on one line; only the trailing
    lambda block or collection expression spans lines (its `{` / `[` may
    open on the next line, Allman style), so the `)` rightly follows its
    `}` / `]`. An unmatched closer is ignored.
    """
    pairs = []
    # Each entry: [char, Paren or None, has_direct_newline]
    stack = []
    line_number = 1
    line_start = 0
    for index, char in enumerate(code):
        if char == "\n":
            next_token = code[index:].lstrip()[:1]
            # Allman braces: a lambda body / initializer / collection whose
            # `{` or `[` opens on the next line has not wrapped the list.
            if stack and next_token not in ("{", "["):
                stack[-1][2] = True
            line_number += 1
            line_start = index + 1
        elif char in "([{":
            paren = (
                Paren(line_number, code[line_start:index], True)
                if char == "("
                else None
            )
            stack.append([char, paren, False])
        elif char in ")]}" and stack:
            opener, opening, is_wrapped = stack.pop()
            if char == ")" and opener == "(":
                closing = Paren(line_number, code[line_start:index], False)
                pairs.append((opening, closing, is_wrapped))
    return pairs


def is_call_or_declaration(text_before_paren: str) -> bool:
    match = IDENTIFIER_BEFORE_PAREN.search(text_before_paren)
    return match is not None and match.group(1) not in CONTROL_KEYWORDS


def is_dangling(opening: Paren, closing: Paren, is_wrapped: bool) -> bool:
    return (
        is_wrapped
        and is_call_or_declaration(opening.text_before)
        and closing.text_before.strip() != ""
    )


def check_file(path: str) -> list:
    with open(path, encoding="utf-8-sig") as fh:
        text = fh.read()
    display = os.path.relpath(path, ROOT).replace(os.sep, "/")
    long_lines = [
        Violation(
            display,
            number,
            LINE_LENGTH_RULE,
            f"{len(line)} characters (max {MAX_LINE_LENGTH})",
        )
        for number, line in enumerate(text.splitlines(), start=1)
        if len(line) > MAX_LINE_LENGTH
    ]
    dangling = [
        Violation(display, closing.line_number, WRAP_RPAR_RULE, WRAP_RPAR_MESSAGE)
        for opening, closing, is_wrapped
        in match_parens(blank_strings_and_comments(text))
        if is_dangling(opening, closing, is_wrapped)
    ]
    return sorted(long_lines + dangling, key=lambda v: v.line_number)


def expand(path: str) -> list:
    if os.path.isfile(path):
        return [path]
    found = []
    for dirpath, dirnames, filenames in os.walk(path):
        dirnames[:] = [d for d in dirnames if d not in EXCLUDED_DIR_NAMES]
        found.extend(
            os.path.join(dirpath, name)
            for name in filenames
            if name.endswith(".cs")
        )
    return sorted(found)


def main(arguments: list) -> int:
    roots = arguments or [ROOT]
    files = [path for root in roots for path in expand(root)]
    violations = [v for path in files for v in check_file(path)]
    for violation in violations:
        print(violation.describe())
    if violations:
        print(f"line layout: {len(violations)} violation(s)", file=sys.stderr)
        return 1
    print(f"line layout: OK ({len(files)} files)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
