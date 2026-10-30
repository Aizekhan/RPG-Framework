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
**Unity Editor:** 6000.0.45f1
**Unity CLI:** 1.0.0-beta.13

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
1. Update `STATUS.md` and `HANDOFF_NEXT_CHAT.md` with actual state (exact SHAs, proof, open gates).
2. Open/update PR in roadmap repository.
3. Short report: what changed, which SHAs verified, what passed, what merged, next step.

## Execution discipline
- Use `/start` command at session beginning.
- Use `/handoff` command when milestone is complete.
- Check active PR state, HEAD SHA, and CI status before continuing work.
- Verify `git status` in all local worktrees before making changes.
- Keep focus on the single ACTIVE roadmap item.
