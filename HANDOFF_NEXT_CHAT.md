# Handoff for Next Session

**Date:** 2026-10-26
**Project:** RPG Framework (`D:\RPG-Framework`, https://github.com/Aizekhan/RPG-Framework)
**Read first:** `STATUS.md` (verified gates, migration table, open issues), then `ARCHITECTURE-GAP-ANALYSIS.md` §6 (migration plan)

---

## Where things stand

- `main` == `origin/main`. Last code-verified SHA: **`8fc25fc`**: clean-checkout compile 0 errors, **1097/1097 EditMode tests**.
- **Active task:** migrating the MythHunter core architecture into the framework. The user approved the plan and asked for work to continue until the whole migration is done. Phases 1–7 are done; **phase 8 (UI navigation) is next**, followed by phase 9.
- CI (`.github/workflows/ci.yml`): the `.meta` integrity job runs. The Unity test job is **skipped** until the `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` secrets exist.
- `main` protection blocks force pushes and branch deletion only. Direct commits to `main` work (per CLAUDE.md).

## What was done

1. Technical audit, plus fixes for every finding (see STATUS.md, "Fixed in the audit").
2. `ARCHITECTURE-GAP-ANALYSIS.md` (`3fb53cb`) compared MythHunter's core with the framework.
3. Migration phases 1–7 (see the STATUS.md table): Logging, Core/UniTask, Events, Systems/World, Bootstrap/GameHost, ECS pools and archetypes, Pooling, AssetManagement, Scenes, Timers, AsyncStateMachine.

## Phase 8/9 starting points

- MythHunter sources: `_MythHunter/Code/UI` (e.g. `UI/Navigation/SceneViewRegistry.cs`). Read the actual code before porting. Port only game-agnostic parts.
- New runtime services follow the existing pattern:
  - Pure C# runtime assembly, plus an optional `*.Unity` assembly for MonoBehaviours.
  - Registered from a `GameFeatureModule` or `FrameworkCoreModule`.
  - Tests in `<Module>/Tests`.

## How to verify (Unity Editor is usually open on the main project)

Run tests in a **separate worktree**, so they don't collide with the open Editor:

```bash
git -C D:/RPG-Framework-verify checkout --detach <sha>   # worktree exists with an imported Library
unity test "D:/RPG-Framework-verify" --mode EditMode --output <xml> --non-interactive
python -I .github/scripts/check_meta.py .
```

Create the worktree with `git worktree add --detach D:/RPG-Framework-verify <sha>`. Remove it with `git worktree remove --force D:/RPG-Framework-verify`.

## Gotchas learned

- Batch-mode Unity exits with code 2 when any test fails. Read the NUnit XML, not the exit code.
- A test assembly whose plugin fails to load reports **0 tests, not an error**. Compare totals between runs (expected: 1097).
- A `.meta` without a valid 32-hex GUID makes Unity ignore the asset, silently. CI catches this.
- Every new file and folder needs a committed `.meta`.
- Stage explicit paths only. The working tree holds user changes that must not be committed (Pooling asmdef, ProjectSettings).
- UniTask:
  - `Preserve()` does not allow several awaiters while the task is pending. Use `UniTaskCompletionSource` for shared in-flight work.
  - Tests rely on synchronous completion and check `.Status`.
- EditMode specifics:
  - `OnDestroy` fires only on `[ExecuteAlways]` behaviours.
  - Use `DestroyImmediate` when `!Application.isPlaying`.
- Avoid namespaces that shadow UnityEngine types (`Resources`, `SceneManagement`, `Time`, `Debug`). Fully qualify `UnityEngine.Time`.
- NuGetForUnity regenerates `Assets/NuGet.config` and `packages.config` on import.

## Decisions waiting for the user

1. Commit or discard the uncommitted EPIC 25 modules (Camera, Debug, Resources, SceneManagement, Time, empty Audio), `Assets/ReferenceGame/`, the NuGet files and `activeInputHandler: 2`. The colliding drafts (`Systems/`, Pooling `PoolManager`/`GameObjectPool`) are preserved in `D:\RPG-Framework-EPIC25-backup`.
2. Addressables: install the package if an Addressables `IAssetProvider` is wanted.
3. Add Unity license secrets for CI.
4. Remove the empty `Core/Installers` module, or give it content.
