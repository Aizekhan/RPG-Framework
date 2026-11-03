"""Check that the repository's documents agree with their single sources of truth.

Each fact has one owner; every other mention must match it, or the check fails:

- package version: Packages/<package>/package.json. The newest released CHANGELOG heading, the `#vX.Y.Z` install
  tags, the TARGET-ARCHITECTURE header and its test base line, the STATUS release line and the top row of Docs/RELEASES.md repeat it.
- Unity Editor version: ProjectSettings/ProjectVersion.txt. CLAUDE.md, STATUS.md, the CI workflow and current docs
  repeat it.
- Unity CLI version: CLAUDE.md only (it lives on the developer's machine, CI has none). With --local the installed
  `unity --version` is compared with it.
- open issues: the register in STATUS.md ("## Open issues", one `- **KI-NN**` bullet each). A CHANGELOG line that
  names a KI closes it, so a KI named in the CHANGELOG must not still be open.
- codebase counts: STATUS.md "**N module folders**, **N asmdef**, **N .cs files**" must match git ls-files.
- modules: a top-level package folder with an .asmdef and no Tests folder must be named in an open issue, or listed
  in TESTED_ELSEWHERE with the folder that tests it.
- package README: every module folder with an .asmdef is named in its "## Modules" table.
- release feed: Packages/<package>/releases.json is generated from the CHANGELOG (`release.py` writes it); a feed that
  differs from what the CHANGELOG gives fails. Games' Claude Code agents read it before updating.
- capabilities manifest: Packages/<package>/capabilities.json (`rpgframework.capabilities/1`, what the framework can do,
  read by WhiteWrite to find a game's gaps). Its version and date are the package's newest release (`release.py` writes
  them), every "since" is a released version (or "unreleased", which `release.py` fills in), ids are unique and keep
  their module's prefix, every "api"/"data" name exists in the code and every "ki" is still open.
- code names: every `TypeName` or `Type.Member` written in backticks in a current document must exist in the code,
  so a renamed or removed API cannot stay in the docs. Made-up names in examples are listed in EXAMPLE_NAMES, outside APIs in EXTERNAL_NAMES.

History is exempt: CHANGELOG.md, Docs/RELEASES.md, Docs/releases/ and Docs/archive/ record what was true then.

Usage: python check_docs.py [project_root] [--local]
"""
import json
import os
import re
import subprocess
import sys

PACKAGE = "Packages/com.aizekhan.rpg-framework"

# Current documents: they describe the framework as it is now
CURRENT_DOCS_ROOT = ["README.md", "START-NEW-GAME.md", "TARGET-ARCHITECTURE.md", "STATUS.md", "HANDOFF_NEXT_CHAT.md",
                     "CLAUDE.md"]
HISTORY = ("CHANGELOG.md", "Docs/RELEASES.md", "Docs/releases/", "Docs/archive/",
           PACKAGE + "/Trading/EPIC19-SUMMARY.md")

# Modules whose tests live in another module's Tests folder
TESTED_ELSEWHERE = {
    "Cards": "Dice/Tests (DiceAndDecksTests)",
}

# Names made up for examples in the guidance (a game's own types, files and tests), not framework code
EXAMPLE_NAMES = {
    "ChestOpenedEvent", "Foo", "DragonPeakSession", "DragonPeakEvents", "ShopChangedEvent", "ItemBoughtEvent",
    "ClearingTheValley_RunsTheWholeEventChain", "XSpawnedEvent", "XDespawnedEvent", "GoblinBrain",
    "GoblinValleyOptions", "GoblinValleyWaveTests", "Obstacles", "Perks", "ObstacleHitEvent",
}

# Outside APIs the docs name (.NET, Unity, UniTask, NUnit); they are not in this repository's code
EXTERNAL_NAMES = {
    "GC", "GetAllocatedBytesForCurrentThread", "Preserve",
}

VERSION_TAG = re.compile(r"#v(\d+\.\d+\.\d+)")
UNITY_VERSION = re.compile(r"\b(6000\.\d+\.\d+f\d+)\b")
CLI_VERSION = re.compile(r"Unity CLI:?\**\s*(\d+\.\d+\.\d+(?:-[a-z]+\.\d+)?)")
KI = re.compile(r"\bKI-\d+\b")
TICKED = re.compile(r"`([^`\n]+)`")
CODE_NAME = re.compile(r"^[A-Z][A-Za-z0-9_]*(?:<[^>`]*>)?(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:\(.*\))?$")
FILE_LIKE = re.compile(r"\.(md|cs|json|asset|asmdef|txt|yml|py|unity|prefab|xml)$")


FEED = PACKAGE + "/releases.json"
CAPABILITIES = PACKAGE + "/capabilities.json"
CAPABILITIES_FORMAT = "rpgframework.capabilities/1"
CAPABILITY_STATUS = ("full", "partial", "planned")
CAPABILITY_ID = re.compile(r"^[a-z0-9_]+(\.[a-z0-9_]+)+$")
FEED_FORMAT = "rpgframework.releases/1"
# A "### Breaking" section in the CHANGELOG marks a version a game must react to; before this version none was written,
# so the feed says null (unknown: read the CHANGELOG) for older ones
BREAKING_FROM = (0, 37, 0)
REFS = re.compile(r"\b(?:FG|BH|KI|WW)-\d+\b")
BULLET = re.compile(r"^- (.+(?:\n  .+)*)", re.MULTILINE)


def plain(text):
    """Markdown bullet text as plain text for the feed."""
    return re.sub(r"\s+", " ", text.replace("**", "")).strip()


def releases_feed(changelog, unity):
    """The machine-readable release feed, generated from the CHANGELOG: newest first, one release per line so a shell
    script can read it without a JSON parser (each line ends with the summary)."""
    lines = []
    for match in re.finditer(r"^## \[(\d+\.\d+\.\d+)\] - (\S+)\s*$(.*?)(?=^## |\Z)", changelog, re.MULTILINE | re.DOTALL):
        version, date, body = match.groups()
        sections = {m.group(1): m.group(2) for m in
                    re.finditer(r"^### (.+?)\s*$(.*?)(?=^### |\Z)", body, re.MULTILINE | re.DOTALL)}
        breaking_section = sections.get("Breaking")
        if breaking_section is not None:
            breaking = True
        elif tuple(map(int, version.split("."))) >= BREAKING_FROM:
            breaking = False
        else:
            breaking = None
        bullets = BULLET.findall(body)
        summary = plain(bullets[0].split("\n")[0]) if bullets else ""
        if len(summary) > 300:
            summary = summary[:297].rstrip() + "..."
        release = {"version": version, "date": date, "breaking": breaking,
                   "refs": sorted(set(REFS.findall(body)), key=lambda r: (r[:2], int(r[3:]))),
                   "upgrade": [plain(b) for b in BULLET.findall(breaking_section or "")], "summary": summary}
        lines.append("    " + json.dumps(release, ensure_ascii=False))
    head = {"format": FEED_FORMAT, "package": PACKAGE.split("/")[-1], "unity": unity,
            "newest": json.loads(lines[0])["version"] if lines else None}
    out = ["{"] + ["  " + json.dumps(k) + ": " + json.dumps(v) + "," for k, v in head.items()]
    out += ['  "releases": [', ",\n".join(lines), "  ]", "}"]
    return "\n".join(out) + "\n"


def capabilities_errors(manifest, version, changelog, names, open_ids):
    """What is wrong with the capabilities manifest (see the module docstring)."""
    errors = []
    dates = dict(re.findall(r"^## \[(\d+\.\d+\.\d+)\] - (\S+)", changelog, re.MULTILINE))
    if manifest.get("format") != CAPABILITIES_FORMAT:
        errors.append(f"format is {manifest.get('format')!r}, expected {CAPABILITIES_FORMAT}")
    if manifest.get("version") != version:
        errors.append(f"version is {manifest.get('version')}, the package is {version}")
    if manifest.get("released") != dates.get(version):
        errors.append(f"released is {manifest.get('released')}, the CHANGELOG dates {version} {dates.get(version)}")
    seen = set()

    def check_entry(entry, where, prefix=None):
        cid = entry.get("id", "")
        if not CAPABILITY_ID.match(cid):
            errors.append(f"{where}: id {cid!r} is not lower_case.dotted")
        if prefix and not cid.startswith(prefix + "."):
            errors.append(f"{where}: id {cid} does not start with its module {prefix}.")
        if cid in seen:
            errors.append(f"{where}: id {cid} appears twice")
        seen.add(cid)
        if not entry.get("title") or not entry.get("notes"):
            errors.append(f"{cid}: needs a title and notes")
        ki = entry.get("ki")
        if ki is not None and ki not in open_ids:
            errors.append(f"{cid}: names {ki}, which is not an open issue in STATUS.md")

    for module in manifest.get("modules", []):
        mid = module.get("id", "")
        if not re.fullmatch(r"[a-z0-9_]+", mid) or mid in seen:
            errors.append(f"module id {mid!r} is not lower_case or appears twice")
        seen.add(mid)
        for cap in module.get("capabilities", []):
            check_entry(cap, f"module {mid}", mid)
            cid = cap.get("id")
            if cap.get("status") not in CAPABILITY_STATUS:
                errors.append(f"{cid}: status {cap.get('status')!r}, expected one of {CAPABILITY_STATUS}")
            since = cap.get("since")
            if since != "unreleased" and since not in dates:
                errors.append(f"{cid}: since {since} is not a released version")
            if cap.get("status") != "planned" and not cap.get("api"):
                errors.append(f"{cid}: lists no api")
            for name in cap.get("api", []) + cap.get("data", []):
                for part in name.split("."):
                    if part not in names:
                        errors.append(f"{cid}: {name}: {part} is not in the code")
    for gap in manifest.get("missing", []):
        check_entry(gap, "missing")
    return errors


def read(root, path):
    with open(os.path.join(root, path), encoding="utf-8") as f:
        return f.read()


def tracked(root):
    out = subprocess.run(["git", "-C", root, "ls-files", "--cached", "--others", "--exclude-standard"], capture_output=True, text=True, encoding="utf-8", check=True)
    return out.stdout.splitlines()


def is_history(path):
    return any(path == h or path.startswith(h) or path.endswith("/" + h) for h in HISTORY)


def codebase_counts(files):
    """Module folders, .asmdef files and .cs files in the package."""
    inside = [f[len(PACKAGE) + 1:] for f in files if f.startswith(PACKAGE + "/")]
    modules = {f.split("/")[0] for f in inside if "/" in f and not f.split("/")[0].endswith("~")}
    return len(modules), sum(f.endswith(".asmdef") for f in inside), sum(f.endswith(".cs") for f in inside)


def section(text, heading):
    match = re.search(r"^## " + re.escape(heading) + r"\s*$(.*?)(?=^## |\Z)", text, re.MULTILINE | re.DOTALL)
    return match.group(1) if match else None


def main(root, local):
    errors = []
    files = tracked(root)
    current = [f for f in files if f.endswith(".md") and not is_history(f)
               and (f in CURRENT_DOCS_ROOT or f.startswith(PACKAGE + "/"))]

    # Package version
    version = json.loads(read(root, PACKAGE + "/package.json"))["version"]
    changelog = read(root, PACKAGE + "/CHANGELOG.md")
    released = re.findall(r"^## \[(\d+\.\d+\.\d+)\]", changelog, re.MULTILINE)
    if not released or released[0] != version:
        errors.append(f"CHANGELOG: newest release is {released[:1]}, package.json says {version}")
    for f in current:
        for n, line in enumerate(read(root, f).splitlines(), 1):
            for tag in VERSION_TAG.findall(line):
                if tag != version:
                    errors.append(f"{f}:{n}: install tag #v{tag}, the package is {version}")
    architecture = read(root, "TARGET-ARCHITECTURE.md")
    header = re.search(r"фактичний стан v(\d+\.\d+\.\d+)", architecture)
    if not header or header.group(1) != version:
        errors.append(f"TARGET-ARCHITECTURE.md: header is for {header and header.group(1)}, the package is {version}")
    tests_line = re.search(r"Тестова база: v(\d+\.\d+\.\d+)", architecture)
    if not tests_line or tests_line.group(1) != version:
        errors.append(f"TARGET-ARCHITECTURE.md: 'Тестова база' is for {tests_line and tests_line.group(1)}, "
                      f"the package is {version}")
    status = read(root, "STATUS.md")
    release_line = re.search(r"^\*\*Release:\*\* `v(\d+\.\d+\.\d+)`", status, re.MULTILINE)
    if not release_line or release_line.group(1) != version:
        errors.append(f"STATUS.md: '**Release:** `v...`' line says {release_line and release_line.group(1)}, "
                      f"the package is {version}")
    counts = re.search(r"\*\*(\d+) module folders\*\*, \*\*(\d+) asmdef\*\*, \*\*(\d+) \.cs files\*\*", status)
    actual = codebase_counts(files)
    if counts and tuple(int(c) for c in counts.groups()) != actual:
        errors.append(f"STATUS.md: codebase counts {counts.groups()}, actual {actual} (module folders, asmdef, .cs)")
    releases = read(root, "Docs/RELEASES.md") if "Docs/RELEASES.md" in files else ""
    top = re.search(r"^\| \*\*Release `v(\d+\.\d+\.\d+)`\*\*", releases, re.MULTILINE)
    if not top or top.group(1) != version:
        errors.append(f"Docs/RELEASES.md: top row is {top and top.group(1)}, the package is {version}")

    # Release feed
    feed = releases_feed(changelog, json.loads(read(root, PACKAGE + "/package.json")).get("unity", ""))
    if FEED not in files or read(root, FEED) != feed:
        errors.append(f"{FEED}: differs from the CHANGELOG; run `python .github/scripts/release.py --feed`")

    # Unity Editor version
    unity = re.search(r"m_EditorVersion:\s*(\S+)", read(root, "ProjectSettings/ProjectVersion.txt")).group(1)
    for f in current + [f for f in files if f.startswith(".github/workflows/")]:
        for n, line in enumerate(read(root, f).splitlines(), 1):
            for found in UNITY_VERSION.findall(line):
                if found != unity:
                    errors.append(f"{f}:{n}: Unity {found}, ProjectVersion.txt says {unity}")

    # Unity CLI version: CLAUDE.md alone
    claude = read(root, "CLAUDE.md")
    cli = CLI_VERSION.search(claude)
    for f in current:
        if f != "CLAUDE.md" and CLI_VERSION.search(read(root, f)):
            errors.append(f"{f}: names a Unity CLI version; only CLAUDE.md does")
    if local and cli:
        try:
            installed = subprocess.run(["unity", "--version"], capture_output=True, text=True, shell=os.name == "nt",
                                       timeout=60).stdout.strip()
            if installed and installed != cli.group(1):
                errors.append(f"CLAUDE.md: Unity CLI {cli.group(1)}, installed {installed}")
        except (OSError, subprocess.TimeoutExpired):
            pass

    # Open issues register
    register = section(status, "Open issues")
    if register is None:
        errors.append("STATUS.md: no '## Open issues' section")
        register = ""
    open_ids = []
    for n, line in enumerate(register.splitlines(), 1):
        if line.startswith("- "):
            match = re.match(r"- \*\*(KI-\d+)\*\*", line)
            if not match:
                errors.append(f"STATUS.md Open issues: a bullet without a KI id: {line[:80]}")
            else:
                open_ids.append(match.group(1))
    for ki in sorted({k for k in open_ids if open_ids.count(k) > 1}):
        errors.append(f"STATUS.md Open issues: {ki} appears twice")
    for ki in sorted(set(open_ids) & set(KI.findall(changelog))):
        errors.append(f"STATUS.md Open issues: {ki} is still open, but the CHANGELOG closes it")

    # Modules without tests
    package_files = [f[len(PACKAGE) + 1:] for f in files if f.startswith(PACKAGE + "/")]
    modules = sorted({f.split("/")[0] for f in package_files if f.endswith(".asmdef") and "/" in f
                      and "Templates~" not in f})
    for module in modules:
        has_tests = any(f.startswith(module + "/") and "/Tests/" in "/" + f for f in package_files)
        if not has_tests and module not in TESTED_ELSEWHERE and not re.search(rf"\b{module}\b", register):
            errors.append(f"module {module} has no Tests folder and no open issue names it")

    # Every module in the package README's module table
    readme_modules = section(read(root, PACKAGE + "/README.md"), "Modules") or ""
    for module in modules:
        if not re.search(r"`" + re.escape(module) + r"[`.( ]", readme_modules):
            errors.append(f"{PACKAGE}/README.md: module {module} is missing from '## Modules'")

    # Code names in backticks
    names = set()
    for f in files:
        if f.endswith(".cs") or f.endswith(".asmdef"):
            names.update(re.findall(r"[A-Za-z_][A-Za-z0-9_]*", read(root, f)))
    for f in current:
        for n, line in enumerate(read(root, f).splitlines(), 1):
            for ticked in TICKED.findall(line):
                if FILE_LIKE.search(ticked) or not CODE_NAME.match(ticked):
                    continue
                bare = re.sub(r"<[^>]*>", "", re.sub(r"\(.*\)$", "", ticked))
                for part in bare.split("."):
                    if (part and not part.isupper() and part not in names and part not in EXAMPLE_NAMES
                            and part not in EXTERNAL_NAMES):
                        errors.append(f"{f}:{n}: `{ticked}`: {part} is not in the code")

    # Capabilities manifest
    if CAPABILITIES not in files:
        errors.append(f"{CAPABILITIES} is missing")
    else:
        try:
            manifest = json.loads(read(root, CAPABILITIES))
        except ValueError as e:
            manifest = None
            errors.append(f"{CAPABILITIES}: not JSON: {e}")
        if manifest is not None:
            errors += [f"{CAPABILITIES}: {e}" for e in
                       capabilities_errors(manifest, version, changelog, names, set(open_ids))]

    for e in errors:
        print("ERROR:", e)
    print(f"check_docs: version {version}, Unity {unity}, {len(current)} current documents, "
          f"{len(open_ids)} open issues, {len(errors)} error(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    # Windows consoles print the Ukrainian document names in their own code page otherwise
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    sys.exit(main(args[0] if args else ".", "--local" in sys.argv))
