"""Validate Unity .meta files under Assets/ and embedded packages (Packages/<name>/ with a package.json).

Fails on:
- .meta files with a missing or malformed GUID (Unity silently ignores the asset)
- duplicate GUIDs
- assets without a .meta file
- .meta files whose asset is missing (orphans; Unity deletes them and churns GUIDs)

Usage: python check_meta.py [project_root]
"""
import os
import re
import sys
from collections import defaultdict

GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)
GUID_LINE_RE = re.compile(r"^guid:.*$", re.MULTILINE)
# Unity skips these names, so they need no .meta
IGNORED = re.compile(r"(^\.|~$|\.tmp$|^cvs$)", re.IGNORECASE)


def main(root):
    roots = [os.path.join(root, "Assets")]
    packages = os.path.join(root, "Packages")
    if os.path.isdir(packages):
        for name in sorted(os.listdir(packages)):
            if os.path.isfile(os.path.join(packages, name, "package.json")):
                roots.append(os.path.join(packages, name))
    errors = []
    guids = defaultdict(list)

    for dirpath, dirnames, filenames in (entry for top in roots for entry in os.walk(top)):
        dirnames[:] = [d for d in dirnames if not IGNORED.search(d)]
        names = set(filenames) | set(dirnames)

        for name in sorted(names):
            if IGNORED.search(name):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, root).replace(os.sep, "/")

            if name.endswith(".meta"):
                asset = name[: -len(".meta")]
                if asset not in names:
                    errors.append(f"orphan .meta (asset missing): {rel}")
                with open(path, encoding="utf-8", errors="replace") as f:
                    text = f.read()
                match = GUID_RE.search(text)
                if match:
                    guids[match.group(1)].append(rel)
                else:
                    line = GUID_LINE_RE.search(text)
                    errors.append(f"invalid GUID in {rel}: {line.group(0) if line else '<no guid line>'}")
            elif name + ".meta" not in names:
                errors.append(f"missing .meta: {rel}")

    for guid, paths in guids.items():
        if len(paths) > 1:
            errors.append(f"duplicate GUID {guid}: {', '.join(paths)}")

    for error in errors:
        print(f"::error::{error}")
    print(f"Checked {sum(len(p) for p in guids.values())} .meta files, {len(errors)} problem(s).")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "."))
