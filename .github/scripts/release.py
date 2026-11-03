"""Write a new package version everywhere it is repeated, in one step, then run check_docs.

The version's owner is package.json; this script is the only writer of its copies:
- package.json "version"
- CHANGELOG: "## [Unreleased]" becomes "## [X.Y.Z] - <date>" under a new empty "## [Unreleased]"
- the `#vX.Y.Z` install tags in current documents (see check_docs.CURRENT_DOCS_ROOT and the package's docs)
- the TARGET-ARCHITECTURE header ("фактичний стан vX.Y.Z", "**vX.Y.Z**", "0.5.1 → X.Y.Z")
- STATUS.md "**Release:** `vX.Y.Z`" line
- a new top row in Docs/RELEASES.md with the proof still to fill in (CI run, test counts)
- the STATUS.md codebase counts (module folders, asmdef, .cs files)
- the release feed releases.json, generated from the CHANGELOG (`--feed` rewrites only the feed)
- capabilities.json "version" and "released", and every "since": "unreleased" becomes the version. What the framework
  can do is written there by hand; a version with "### Added" whose capabilities did not change since the last tag is
  refused unless `--same-capabilities` says so on purpose

What changed in this version (TARGET-ARCHITECTURE text, the release row's summary, STATUS open issues) is still written
by hand in the same release commit; check_docs fails on whatever was left behind.

Usage: python release.py X.Y.Z [project_root] [--same-capabilities]
       python release.py --feed [project_root]
"""
import datetime
import json
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import check_docs  # noqa: E402

PACKAGE = check_docs.PACKAGE


def edit(root, path, change):
    full = os.path.join(root, path)
    with open(full, encoding="utf-8", newline="") as f:
        text = f.read()
    new = change(text)
    if new != text:
        with open(full, "w", encoding="utf-8", newline="") as f:
            f.write(new)
        print("updated", path)


def write_feed(root):
    unity = json.loads(check_docs.read(root, PACKAGE + "/package.json")).get("unity", "")
    feed = check_docs.releases_feed(check_docs.read(root, PACKAGE + "/CHANGELOG.md"), unity)
    full = os.path.join(root, check_docs.FEED)
    if not os.path.isfile(full) or check_docs.read(root, check_docs.FEED) != feed:
        with open(full, "w", encoding="utf-8", newline="") as f:
            f.write(feed)
        print("updated", check_docs.FEED)


def capabilities_body(text):
    """The manifest without the fields this script writes, to see whether anyone described the version's changes."""
    manifest = json.loads(text)
    manifest.pop("version", None)
    manifest.pop("released", None)
    return json.dumps(manifest, sort_keys=True)


def capabilities_unchanged(root, old):
    shown = subprocess.run(["git", "-C", root, "show", f"v{old}:{check_docs.CAPABILITIES}"], capture_output=True,
                           text=True, encoding="utf-8")
    if shown.returncode != 0:
        return False
    return capabilities_body(shown.stdout) == capabilities_body(check_docs.read(root, check_docs.CAPABILITIES))


def main(version, root, same_capabilities=False):
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        sys.exit("version must be X.Y.Z")
    today = datetime.date.today().isoformat()

    # Every precondition before the first write, so a refusal leaves nothing half done
    old = json.loads(check_docs.read(root, PACKAGE + "/package.json"))["version"]
    if tuple(map(int, version.split("."))) <= tuple(map(int, old.split("."))):
        sys.exit(f"{version} is not newer than package.json's {old}")
    if not re.search(r"^## \[Unreleased\]\s*\n(?!\s*## )\s*\S", check_docs.read(root, PACKAGE + "/CHANGELOG.md"),
                     re.MULTILINE):
        sys.exit("CHANGELOG: nothing under [Unreleased]")
    if not os.path.isfile(os.path.join(root, "Docs/RELEASES.md")):
        sys.exit("Docs/RELEASES.md is missing")
    unreleased = check_docs.read(root, PACKAGE + "/CHANGELOG.md").split("## [Unreleased]", 1)[1].split("\n## [", 1)[0]
    if "### Added" in unreleased and not same_capabilities and capabilities_unchanged(root, old):
        sys.exit(f"{check_docs.CAPABILITIES}: the CHANGELOG adds things, but the capabilities are the same as in v{old}. "
                 "Describe what this version adds there, or pass --same-capabilities if it adds no capability")

    edit(root, PACKAGE + "/package.json",
         lambda t: re.sub(r'("version":\s*")[^"]+(")', rf"\g<1>{version}\g<2>", t, count=1))
    edit(root, PACKAGE + "/CHANGELOG.md",
         lambda t: t.replace("## [Unreleased]", f"## [Unreleased]\n\n## [{version}] - {today}", 1))

    files = check_docs.tracked(root)
    current = [f for f in files if f.endswith(".md") and not check_docs.is_history(f)
               and (f in check_docs.CURRENT_DOCS_ROOT or f.startswith(PACKAGE + "/"))]
    for f in current:
        edit(root, f, lambda t: check_docs.VERSION_TAG.sub(f"#v{version}", t))

    def architecture(text):
        text = re.sub(r"фактичний стан v\d+\.\d+\.\d+", f"фактичний стан v{version}", text, count=1)
        text = re.sub(r"\*\*v\d+\.\d+\.\d+\*\*", f"**v{version}**", text, count=1)
        text = re.sub(r"\(тег `v\d+\.\d+\.\d+`", f"(тег `v{version}`", text, count=1)
        return re.sub(r"0\.5\.1 → \d+\.\d+\.\d+", f"0.5.1 → {version}", text, count=1)

    edit(root, "TARGET-ARCHITECTURE.md", architecture)

    modules, asmdefs, sources = check_docs.codebase_counts(check_docs.tracked(root))

    def status(text):
        text = re.sub(r"^\*\*Release:\*\* `v\d+\.\d+\.\d+`.*$",
                      f"**Release:** `v{version}` ({today}; proof in `Docs/RELEASES.md`)", text, count=1,
                      flags=re.MULTILINE)
        text = re.sub(r"^\*\*Last Updated:\*\*.*$", f"**Last Updated:** {today}", text, count=1, flags=re.MULTILINE)
        return re.sub(r"\*\*\d+ module folders\*\*, \*\*\d+ asmdef\*\*, \*\*\d+ \.cs files\*\*",
                      f"**{modules} module folders**, **{asmdefs} asmdef**, **{sources} .cs files**", text, count=1)

    edit(root, "STATUS.md", status)
    write_feed(root)

    def capabilities(text):
        text = re.sub(r'^(  "version":\s*")[^"]*(")', rf"\g<1>{version}\g<2>", text, count=1, flags=re.MULTILINE)
        text = re.sub(r'^(  "released":\s*")[^"]*(")', rf"\g<1>{today}\g<2>", text, count=1, flags=re.MULTILINE)
        return text.replace('"since": "unreleased"', f'"since": "{version}"')

    edit(root, check_docs.CAPABILITIES, capabilities)

    def releases(text):
        row = (f"| **Release `v{version}`** (tag on `TODO`; TODO: who asked): TODO: what it brings. Code `TODO`. "
               f"EditMode **TODO** locally | TODO: CI run | `TODO` |\n")
        marker = re.search(r"^\|---.*\n", text, re.MULTILINE)
        return text[:marker.end()] + row + text[marker.end():]

    edit(root, "Docs/RELEASES.md", releases)

    print("\nNow fill the TODOs in Docs/RELEASES.md, update TARGET-ARCHITECTURE.md and the STATUS.md open issues, "
          "then commit 'chore: release " + version + "'.")
    return check_docs.main(root, local=False)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    if sys.argv[1] == "--feed":
        write_feed(sys.argv[2] if len(sys.argv) > 2 else ".")
        sys.exit(0)
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    sys.exit(main(args[0], args[1] if len(args) > 1 else ".", "--same-capabilities" in sys.argv))
