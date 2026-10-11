#!/usr/bin/env python3
"""Rename a project across the repository.

    tools/rename-project.py OLD NEW [--dry-run]

OLD and NEW are project names (for example Crest.Workflows.Platform.Abstractions and
Crest.Workflows.Abstractions). The script, run from the repository root:

1. `git mv`s the project folder under src/ (or test/) and the .csproj inside it.
2. Replaces every textual occurrence of OLD with NEW in tracked text files: project
   references, solution files, props, namespaces and usings, docs. Namespaces that
   merely start with OLD's prefix (RootNamespace) are left alone; only the exact
   dotted name changes.
3. Prints what it touched. Nothing is committed.

Binary files, bin/ and obj/ folders and .po translation files are skipped.
"""

import re
import subprocess
import sys
from pathlib import Path

SKIP_SUFFIXES = {".po", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".woff", ".woff2", ".ttf", ".dll", ".pdb", ".zip", ".nupkg", ".snk"}
SKIP_PARTS = {"bin", "obj", ".git", "node_modules"}


def run(*args):
    return subprocess.run(args, check=True, capture_output=True, text=True).stdout


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    dry = "--dry-run" in sys.argv
    if len(args) != 2:
        sys.exit(__doc__)
    old, new = args
    root = Path(run("git", "rev-parse", "--show-toplevel").strip())

    # 1. folder and csproj
    folder = next((p for p in [root / "src" / old, root / "test" / old] if p.is_dir()), None)
    if folder is None:
        sys.exit(f"no project folder named {old} under src/ or test/")
    new_folder = folder.with_name(new)
    if new_folder.exists():
        sys.exit(f"{new_folder} already exists")
    print(f"move {folder.relative_to(root)} -> {new_folder.relative_to(root)}")
    if not dry:
        run("git", "mv", str(folder), str(new_folder))
        old_csproj = new_folder / f"{old}.csproj"
        if old_csproj.exists():
            run("git", "mv", str(old_csproj), str(new_folder / f"{new}.csproj"))

    # 2. text replacement in tracked files
    pattern = re.compile(re.escape(old) + r"(?![A-Za-z0-9_])")
    tracked = run("git", "ls-files", "-z").split("\0")
    touched = 0
    for rel in tracked:
        if not rel:
            continue
        path = root / rel
        if any(part in SKIP_PARTS for part in path.parts) or path.suffix.lower() in SKIP_SUFFIXES:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, FileNotFoundError):
            continue
        if not pattern.search(text):
            continue
        touched += 1
        print(f"edit {rel} ({len(pattern.findall(text))})")
        if not dry:
            path.write_text(pattern.sub(new, text), encoding="utf-8")
    print(f"{touched} files edited{' (dry run)' if dry else ''}")


if __name__ == "__main__":
    main()
