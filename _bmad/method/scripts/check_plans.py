#!/usr/bin/env python3
"""Check that plans fill the evidence fields the plan template asks for.

A plan is a markdown file in the ticket tree whose frontmatter has a `ticket` key. Plans that
have left `draft` (and are not `dropped`) must:

- set a non-empty `baseline_revision` (the commit the work starts from), and
- contain a `## Verification` section.

Exit code 0 when every checked plan passes, 1 when any plan fails, 2 on usage errors.

Usage:
    uv run _bmad/method/scripts/check_plans.py --project-root <root> [--initiatives-dir <dir>]
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from tickets import parse_frontmatter, read_text  # noqa: E402

EXEMPT_STATUSES = {"draft", "dropped"}
VERIFICATION_RE = re.compile(r"^## Verification\s*$", re.MULTILINE)


def is_plan(text: str) -> bool:
    return "ticket" in parse_frontmatter(text, lenient=True)


def check_plan(text: str) -> list[str]:
    fields = parse_frontmatter(text, lenient=True)
    if str(fields.get("status", "")).strip() in EXEMPT_STATUSES:
        return []
    problems = []
    if not str(fields.get("baseline_revision", "") or "").strip():
        problems.append("baseline_revision is empty")
    if not VERIFICATION_RE.search(text):
        problems.append("missing '## Verification' section")
    return problems


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--initiatives-dir", default="_bmad-output", help="relative to project root")
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    base = root / args.initiatives_dir
    if not base.is_dir():
        print(f"error: {base} is not a directory", file=sys.stderr)
        return 2

    checked = 0
    failed = 0
    for path in sorted(base.rglob("*.md")):
        if path.name.endswith("-retrospective.md"):
            continue
        text = read_text(path)
        if not is_plan(text):
            continue
        problems = check_plan(text)
        checked += 1
        rel = path.relative_to(root).as_posix()
        if problems:
            failed += 1
            print(f"FAIL {rel}")
            for problem in problems:
                print(f"  - {problem}")
        else:
            print(f"ok   {rel}")

    print(f"{checked} plan(s) checked, {failed} failing")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
