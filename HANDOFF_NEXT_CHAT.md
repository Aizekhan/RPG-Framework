# Handoff for Next Session

**Date:** 2026-10-26
**Project:** RPG Framework (`D:\RPG-Framework`, https://github.com/Aizekhan/RPG-Framework)
**Read first:** `STATUS.md` (verified gates, migration table, open issues), then `ARCHITECTURE-GAP-ANALYSIS.md` §6 (migration plan)

---

## Where things stand

- `main` == `origin/main`. Last code-verified SHA: **`35a1dbb`**: clean-checkout compile 0 errors, **1170/1170 EditMode tests**.
- **The MythHunter core migration (phases 1–9) is complete.** See the STATUS.md table for every SHA. No active roadmap item; next candidates are in STATUS.md "Next steps".
- CI (`.github/workflows/ci.yml`): `.meta` integrity and GameCI EditMode tests both run on every push (license secrets set 2026-10-26; first green run `40337490738`, 1170/1170).
- `main` protection blocks force pushes and branch deletion only. Direct commits to `main` work (per CLAUDE.md).

## What was done

1. Technical audit, plus fixes for every finding (see STATUS.md, "Fixed in the audit").
2. `ARCHITECTURE-GAP-ANALYSIS.md` (`3fb53cb`) compared MythHunter's core with the framework.
3. Migration phases 1–7 (see the STATUS.md table): Logging, Core/UniTask, Events, Systems/World, Bootstrap/GameHost, ECS pools and archetypes, Pooling, AssetManagement, Scenes, Timers, AsyncStateMachine.
4. Phase 8: `UINavigation` module. Phase 9: loot builder, save coordinator (+ load data-loss fix), network authority, diagnostics, DI optional parameters, module events, Combat→Quests bridge.

## Conventions for new work

- Gameplay services take an optional `IEventPublisher events = null` (DI resolves it when registered) and publish their module's `IEvent` structs.
- Cross-module glue lives in optional `Integration/<A><B>` assemblies, never as a direct reference between gameplay modules.
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
- A test assembly whose plugin fails to load reports **0 tests, not an error**. Compare totals between runs (expected: 1170).
- Test assemblies that construct `EventBus` need `RPGFramework.Logging.Runtime` (CS0012 otherwise).
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
3. Remove the empty `Core/Installers` module, or give it content.
