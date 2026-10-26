# Handoff for Next Session

**Date:** 2026-10-26
**Project:** RPG Framework (`D:\RPG-Framework`, https://github.com/Aizekhan/RPG-Framework)
**Read first:** `STATUS.md` (verified gates, migration table, open issues), then `ARCHITECTURE-GAP-ANALYSIS.md` §6 (migration plan)

---

## Where things stand

- Last code-verified SHA: **`6eca982`**: clean-checkout compile 0 errors, **1243/1243 EditMode**, **4/4 PlayMode**, CI green.
- Goblin Valley (`Assets/ReferenceGame`) is now an `ActionRpg` game: assets in `Content/`, one coded rule (`KillStreak`), rebuilt by `GoblinValleyBuilder` in batch mode.
- The user story is delivered: create a Unity project, add the package, **RPG Framework > Game Kit > New Game**, Play: a hero walks and fights goblin waves with a HUD, character sheet (I/Tab) and saved progress. New enemies/items/quests are assets (`Character`, `Game Setup`, Item, Loot Table, Quest Asset); code only for unique rules (`ActionRpgHost.ConfigureGameModules`, events on the bus).
- Proven in a fresh project `D:/RPG-Fresh` (manifest points at the package in `D:/RPG-Framework-verify`, so its contents follow that worktree).
- Leftovers of an old New Game run (`Assets/SampleQuest`, `Docs/`, `.github/workflows/unity-tests.yml`, a CLAUDE.md block) were removed from the main tree at the user's request; a copy is in the session scratchpad `leftovers-backup`.

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

- Game layer split: rules in plain C# (`Scripts/Core`), announced on the bus; views in `Scripts/View` bound to entities through `EntityViewRegistry`. Views only read state and call the game API.
- Generated assets (prefabs, scene) come from an editor builder run in batch mode in the verify worktree, then copied back:
  `Unity.exe -batchmode -quit -nographics -projectPath D:/RPG-Framework-verify -executeMethod ReferenceGame.Editor.GoblinValleyBuilder.Rebuild -logFile <log>`

## How to verify (Unity Editor is usually open on the main project)

Run tests in a **separate worktree**, so they don't collide with the open Editor:

```bash
git -C D:/RPG-Framework-verify checkout --detach <sha>   # worktree exists with an imported Library
unity test "D:/RPG-Framework-verify" --mode EditMode --output <xml> --non-interactive
unity test "D:/RPG-Framework-verify" --mode PlayMode --output <xml> --non-interactive
python -I .github/scripts/check_meta.py .
```

Create the worktree with `git worktree add --detach D:/RPG-Framework-verify <sha>`. Remove it with `git worktree remove --force D:/RPG-Framework-verify`.

## Gotchas learned

- Batch-mode Unity exits with code 2 when any test fails. Read the NUnit XML, not the exit code.
- A test assembly whose plugin fails to load reports **0 tests, not an error**. Compare totals between runs (expected: 1243 EditMode, 4 PlayMode).
- The verify worktree gets NuGetForUnity files generated on import; remove them there before `checkout`, or checkout aborts and the old commit is tested. Check the SHA the run actually used.
- A MonoBehaviour must live in a file of its own name (enforced by `FrameworkArchitectureTests`). Test MonoBehaviours go in a `*.TestSupport` runtime assembly.
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

- `EditorSceneManager.OpenScene(..., Single)` unloads unused assets: reload asset references created before it.
- `verify.sh` checks out before cleaning: files copied into the worktree by hand block the checkout. Run `git checkout -- . && git clean -fdq -e Library` there first.
- Folders ending in `~` (`Templates~`, `Documentation~`) are not imported by Unity and need no metas; `gen_meta.py` skips them.
- Large bash heredocs with apostrophes fail to parse in this environment; write files with the Write tool.
- Logging extension methods (`LogError`, ...) need `using RPGFramework.Logging;`.

## Decisions waiting for the user

- Networking: is co-op in scope, and Unity Transport as the first transport? (`Documentation~/networking.md`)
