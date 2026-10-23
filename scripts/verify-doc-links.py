#!/usr/bin/env python3
"""
Every relative link in docs/**, the root README.md, and every package's own
README.md must resolve.

For every `](target)` in each of those files:
  - a link to a file (optionally with a `#anchor`) must point at a real path,
    resolved relative to the containing file;
  - a bare `#anchor` (no file part - a same-page reference) is checked against
    the containing file's own headings;
  - if a file link carries a `#anchor`, the target file must have a heading
    whose GitHub slug matches.

`http(s)://` and `mailto:` links are not checked. Exit non-zero (listing every
break) on the first unresolved link.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(ROOT, "docs")
LINK_RE = re.compile(r"\]\(([^)]+)\)")
HEADING_RE = re.compile(r"^#{1,6}\s+(.*?)\s*$")

# Directories that never hold a README.md worth checking, or whose own copy
# of one is a build artifact rather than a source file.
PRUNE_DIR_NAMES = {
    "bin", "obj", ".git", ".vs", ".idea", "node_modules", "docs", "TestResults",
}


def slug(heading: str) -> str:
    # GitHub's algorithm: lowercase, drop anything that is not word/space/hyphen,
    # spaces to hyphens.
    s = heading.strip().lower()
    s = re.sub(r"[^\w\s-]", "", s)
    return s.replace(" ", "-")


def headings_of(path: str) -> set:
    out = set()
    with open(path, encoding="utf-8") as fh:
        in_fence = False
        for line in fh:
            if line.lstrip().startswith("```"):
                in_fence = not in_fence
                continue
            if in_fence:
                continue
            m = HEADING_RE.match(line)
            if m:
                out.add(slug(m.group(1)))
    return out


def doc_pages() -> list:
    pages = []
    for dirpath, _, filenames in os.walk(DOCS):
        for name in filenames:
            if name.endswith(".md"):
                pages.append(os.path.join(dirpath, name))

    # The root README, plus every package's own README - not just docs/.
    pages.append(os.path.join(ROOT, "README.md"))
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in PRUNE_DIR_NAMES and not d.startswith(".")]
        if dirpath == ROOT:
            continue  # the root README is already added above
        if "README.md" in filenames:
            pages.append(os.path.join(dirpath, "README.md"))
    return pages


def main() -> int:
    heading_cache = {}
    broken = []
    checked = 0

    for page in doc_pages():
        dirpath = os.path.dirname(page)
        with open(page, encoding="utf-8") as fh:
            lines = fh.readlines()
        in_fence = False
        for lineno, line in enumerate(lines, 1):
            if line.lstrip().startswith("```"):
                in_fence = not in_fence
                continue
            if in_fence:
                continue
            for target in LINK_RE.findall(line):
                target = target.strip()
                if target.startswith(("http://", "https://", "mailto:")):
                    continue
                checked += 1
                path_part, _, anchor = target.partition("#")
                dest = os.path.normpath(os.path.join(dirpath, path_part)) if path_part else os.path.normpath(page)
                if not os.path.exists(dest):
                    broken.append(f"{os.path.relpath(page, ROOT)}:{lineno}  missing file  ->  {target}")
                    continue
                if anchor and dest.endswith(".md"):
                    if dest not in heading_cache:
                        heading_cache[dest] = headings_of(dest)
                    if slug(anchor) not in heading_cache[dest]:
                        broken.append(f"{os.path.relpath(page, ROOT)}:{lineno}  missing anchor  ->  {target}")

    print(f"checked {checked} relative links across docs/, README.md, and every package README")
    if broken:
        print(f"\n{len(broken)} broken link(s):\n")
        print("\n".join("  " + b for b in broken))
        return 1
    print("every relative link and anchor resolves")
    return 0


if __name__ == "__main__":
    sys.exit(main())
