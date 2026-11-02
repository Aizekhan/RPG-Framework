# Claude Code Process — RPG Framework

> Permanent rules for Claude Code when working on RPG Framework project.

## Mission
Build a **universal RPG Framework** that is reusable across any RPG game.

**Framework Core principles:**
- Framework Core does NOT depend on ANY game-specific logic, UnityEditor, or game policies
- Framework provides systems, contracts, and building blocks
- Game Layer provides content, configuration, and game-specific implementations
- No empty modules, broad refactorings, or duplication of single source of truth

**Five filters for every change:**
1. Is it needed by ANY RPG game (not just one specific game)?
2. Does it depend on game-specific logic? (If YES, belongs in Game Layer)
3. Is it reusable across different RPG games?
4. Is it Framework Core or Game Layer?
5. Is there proof of the problem being solved?

## Git guardrails
**Prohibited operations:**
- `git reset --hard`, `git clean`, `git stash` without analyzing working tree
- `git cherry-pick` without analyzing working tree
- Deleting user files, NUnit XML results, `.meta` files, or `.vscode` configuration

**Workflow:**
- Work directly on `main` branch for now (no feature branches yet)
- Commit frequently with descriptive messages
- Use conventional commit format: `feat:`, `fix:`, `docs:`, `test:`, etc.
- Never merge or delete without explicit user confirmation

## Unity environment
**Unity Editor:** 6000.0.45f1 (owner: `ProjectSettings/ProjectVersion.txt`)
**Unity CLI:** 1.0.0-beta.15 (this line is the only place that names it; `check_docs.py --local` compares it with `unity --version`)

**Standard commands:**
```bash
# Recompile project
unity recompile --project-path "<path>" --timeout 180 --verbose

# Run EditMode tests
unity test "<path>" --mode EditMode --output "<xml>" --verbose --timeout <seconds>
```

**Log locations:**
- Editor log: `C:\Users\Admin\AppData\Local\Unity\Editor\Editor.log`
- CLI log: `C:\Users\Admin\AppData\Roaming\UnityHub\logs\cli-log.json`

**Unity CLI guardrails:**
- Do NOT kill Unity CLI Server process (`unity.exe ... serve`).
- Do NOT delete Unity caches without proven cause.
- Do NOT repeat long-running commands without a new hypothesis.
- Run long-running commands in background with log monitoring.

## Proof and validation
**Test/build result validity:**
- Results are valid ONLY for the exact commit SHA tested
- All validation must be done on clean Framework project
- No game-specific code in validation

**Unity validation sequence:**
1. Unity EditMode tests with NUnit XML report
2. Full project compile with clean Console (0 errors)
3. Framework independence validation (no game dependencies)
4. Performance benchmarks

## Single source of truth for documents
Every fact has one owner; every copy is checked against it by `.github/scripts/check_docs.py`, which runs in CI
(job "Docs agree with their sources") and locally with `python -I .github/scripts/check_docs.py . --local`.
The owners are listed in `STATUS.md` ("Single sources of truth"). The rules:
- `STATUS.md` holds the **current state only**; history goes to `Docs/RELEASES.md` (proof of each release, append-only)
  and `Docs/archive/SESSION-LOG.md` (session notes). Never leave a "not done" claim in a current document after the
  change that does it.
- Everything open, missing or limited is a `KI-NN` bullet in STATUS "Open issues", and nowhere else as a current claim.
  A change that closes one names the KI in its CHANGELOG line and deletes the bullet in the same commit.
- A new version is written only by `python .github/scripts/release.py X.Y.Z` (package.json, CHANGELOG heading, install
  tags, TARGET-ARCHITECTURE header, STATUS release line, `Docs/RELEASES.md` row, codebase counts).
- A change a game must react to (renamed or removed API, changed default, new required step) goes under
  `### Breaking` in its CHANGELOG version, one bullet per thing the game does. The release feed
  (`Packages/com.aizekhan.rpg-framework/releases.json`, which games' agents read before updating) is generated from the
  CHANGELOG by `release.py` and checked; never edit it by hand.
- Docs name real code: a backticked `Type` or `Type.Member` that is not in the code fails the check (made-up example
  names go in `EXAMPLE_NAMES`, outside APIs in `EXTERNAL_NAMES`).
- Run the check before every commit that touches documents, versions or modules; a red check is fixed, never bypassed.

## Session process
**On session start:**
1. Read `STATUS.md` and `HANDOFF_NEXT_CHAT.md` from project root (`D:\RPG-Framework\`)
2. Verify current phase and active tasks
3. When documents contradict, reconcile them BEFORE implementation
4. Read `FRAMEWORK-GAPS.md` of every game that uses the framework (READ ONLY, never edit those repos), so gaps and bugs are seen even when a cross-session message was missed:
   - `D:\HuntersRunner\FRAMEWORK-GAPS.md` (MythRunner)
   - `D:\The Bloodline Hunt\FRAMEWORK-GAPS.md` (The Bloodline Hunt)
   - `D:\RPG-Games\Kurhan\FRAMEWORK-GAPS.md` (Kurhan, demo on v0.18.0)
   - `D:\RPG-Games\EchoTower\FRAMEWORK-GAPS.md` (Echo Tower, demo on v0.18.0)
   - `D:\RPG-Games\SaltRoad\FRAMEWORK-GAPS.md` (The Salt Road, demo on v0.18.0)
   - `D:\RPG-Games\AshenDeck\FRAMEWORK-GAPS.md` (Ashen Deck, demo on v0.18.0)
   - `D:\RPG-Games\CrossroadsInn\FRAMEWORK-GAPS.md` (Crossroads Inn, demo on v0.18.0)
   - `D:\RPG-Games\Potioneer\FRAMEWORK-GAPS.md` (Potioneer, demo on v0.18.0)
   - `D:\RPG-Games\Homestead\FRAMEWORK-GAPS.md` (Homestead, demo on v0.18.0)
   - `D:\RPG-Games\BoneRoad\FRAMEWORK-GAPS.md` (Bone Road, demo on v0.18.0)
   - `D:\RPG-Games\Stepdeep\FRAMEWORK-GAPS.md` (Stepdeep, demo on v0.18.0)
   - `D:\RPG-Games\GreycloakGuild\FRAMEWORK-GAPS.md` (Greycloak Guild, demo on v0.18.0)
   - `D:\RPG-Games\README.md` — gaps confirmed by several of these demos (strongest framework candidates)

   When a new game or project starts using the framework, add its `FRAMEWORK-GAPS.md` to this list.

**During session:**
- Track work with TodoWrite tool.
- Use AskUserQuestion tool when clarification is needed.
- Maintain exactly ONE in_progress todo at a time.

**At milestone completion:**
1. Release commit (`chore: release X.Y.Z`), all in one commit: `release.py X.Y.Z`; the `Docs/RELEASES.md` row (what,
   who asked, local test counts); `TARGET-ARCHITECTURE.md` text from the new version's `CHANGELOG.md`
   ([є]/[частково]/[немає] marks, test base line), in Ukrainian, describing the framework only; STATUS open issues the
   version closes or opens; `check_docs.py` green. Then tag and push.
2. After CI on the tag: a `docs:` commit fills the proof only (tag SHA, CI run) in `Docs/RELEASES.md`, and the latest
   session notes in `HANDOFF_NEXT_CHAT.md` (older notes move to `Docs/archive/SESSION-LOG.md`).
3. Open/update PR in roadmap repository.
4. Short report: what changed, which SHAs verified, what passed, what merged, next step.

## Execution discipline
- Use `/start` command at session beginning.
- Use `/handoff` command when milestone is complete.
- Check active PR state, HEAD SHA, and CI status before continuing work.
- Verify `git status` in all local worktrees before making changes.
- Keep focus on the single ACTIVE roadmap item.
