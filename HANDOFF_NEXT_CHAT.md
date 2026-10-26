# Handoff for Next Session

**Date:** 2026-10-26
**Project:** RPG Framework (`D:\RPG-Framework`, https://github.com/Aizekhan/RPG-Framework)
**Read first:** `STATUS.md` (verified gates, open issues)

---

## Where things stand

- `main` == `origin/main`. Last code-verified SHA: **`03d28e0`**: clean-checkout compile 0 errors, **888/888 EditMode tests**.
- CI (`.github/workflows/ci.yml`) is green, but the Unity test job is **skipped** until the `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` secrets exist. Only the `.meta` integrity job really runs.
- `main` protection blocks force pushes and branch deletion only. Direct commits to `main` still work (per CLAUDE.md).

## What this session did

The session started with a technical audit, then went through its fixes in order. Each fix was verified on a fresh worktree at the exact SHA. Details are in the STATUS.md table:
- Fixed HEAD `e348e10`, which did not compile from a clean clone (Tools runtime files were deleted, their replacements never committed).
- Re-enabled ~150 tests that were silently not running: Guilds (missing GUIDs) and Quests/Dialogue/Crafting (Moq failed to load).
- Fixed real bugs: `TimeSystem` start time, `DisbandGuild` leader, `EventParticipant` state, JSON culture data loss.
- Removed `BinaryFormatter` (security).
- Added CI with a `.meta` checker (`.github/scripts/check_meta.py`).

## How to verify (Unity Editor may be open on the main project)

Run tests in a **separate worktree**, so they don't collide with the open Editor:

```bash
git worktree add --detach D:/RPG-Framework-verify <sha>   # first time; afterwards: git -C D:/RPG-Framework-verify checkout --detach <sha>
unity test "D:/RPG-Framework-verify" --mode EditMode --output <xml> --non-interactive
python -I .github/scripts/check_meta.py .
```

`D:/RPG-Framework-verify` currently exists, with an imported Library, for fast reruns. Remove it with `git worktree remove --force D:/RPG-Framework-verify`.

## Gotchas learned

- Batch-mode Unity exits with code 2 when any test fails. Read the NUnit XML, not the exit code.
- A test assembly whose plugin fails to load reports **0 tests, not an error**. Compare total counts between runs (expected: 888).
- A `.meta` without a valid 32-hex GUID makes Unity ignore the asset, again silently. CI now catches this.
- NuGetForUnity regenerates `Assets/NuGet.config` and `packages.config` on import.

## Decisions waiting for the user

1. Commit or discard the uncommitted EPIC 25 modules (Camera, Debug, Resources, SceneManagement, Systems, Time, empty Audio), Pooling `GameObjectPool`/`PoolManager`, `Assets/ReferenceGame/`, NuGet files, and `activeInputHandler: 2`.
2. Remove the empty `Core/Installers` module, or give it content.
3. Add Unity license secrets for CI.

## Suggested next task

Resolve the duplicate types (StatusEffects vs Combat, Quests vs Tools/QuestEditor, `StatModifier`, `EquipmentSlot`), then fix test asmdef hygiene. See STATUS.md, "Open issues".
