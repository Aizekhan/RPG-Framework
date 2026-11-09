# Handoff for Next Session

**Date:** 2026-11-06
**Project:** RPG Framework (`D:\RPG-Framework`, https://github.com/Aizekhan/RPG-Framework)
**Read first:** `STATUS.md` (current state, single sources of truth, open issues `KI-NN`, next steps), `TARGET-ARCHITECTURE.md`, `Docs/RELEASES.md` (proof of each release). Session history: `Docs/archive/SESSION-LOG.md`

---

## Where things stand

This section holds only the latest session's notes; the release itself is in `STATUS.md` and `Docs/RELEASES.md`,
older notes move to `Docs/archive/SESSION-LOG.md`.

- 2026-11-09: released `v0.46.0` (the owner asked for a standard fog of war module for any game, not only ours;
  Stepdeep SD-03 asked for it): `FogOfWar` (`FogOfWarMap` per team with eyes counted per cell, `FieldOfView`
  symmetric shadowcasting, `GridSight`, `SightBlockers`, `FogGridSpace` for 3D worlds, `FogOfWarSave`,
  `FogOfWarModule`; Unity `FogOfWarTexture`, `FogOfWarEye`). KI-27 opened (square grids only; co-op fog message is
  the game's). The machine was short of memory (The Bloodline Hunt batch jobs ran alongside): one local run hit
  the 1800 s timeout while compiling, one was stopped by Claude Code; use `--timeout 5400` when other Unity jobs run.
  Stepdeep can replace its `Sight.cs` stand-in after updating.
- 2026-11-09: released `v0.45.0` (The Bloodline Hunt BH-11, its agent's note relayed by the owner): click to move
  in the action RPG kit. `PlayerController.ClickToMove` (off by default): a click on the ground walks there, on an
  enemy walks up and attacks, holding follows the pointer, WASD takes over; `MoveTo`, `MoveToAttack`, `Stop`,
  virtual `MoveInput` / `TryPick`. The way goes round obstacles on the scene's NavMesh (`GroundPath`), so the game
  bakes one; the package now depends on `com.unity.modules.ai`. Earlier the owner asked about GLB import: the
  framework has none (MythRunner uses glTFast 6.20.0 itself), which is the game's choice.
- 2026-11-06: released `v0.44.0` (MythRunner FG-34, FG-35, read from its `FRAMEWORK-GAPS.md`; no MythRunner
  session was running to tell): a start menu names how to play together (`GameStart.Host.Via`,
  `GameStart.Join(code, provider)`, `NetworkSessionProviders.IsAvailable`); new `WorldMap` module (`WorldGraph`,
  `IWorldTravel`, `WorldTravelSave`, `WorldMapModule`). KI-26 opened (co-op world-map message and place-grouped
  interaction points are the game's).

## Conventions for new work

- Gameplay services take an optional `IEventPublisher events = null` (DI resolves it when registered) and publish their module's `IEvent` structs.
- Cross-module glue lives in optional `Integration/<A><B>` assemblies, never as a direct reference between gameplay modules.
- New runtime services follow the existing pattern:
  - Pure C# runtime assembly, plus an optional `*.Unity` assembly for MonoBehaviours.
  - Registered from a `GameFeatureModule` or `FrameworkCoreModule`.
  - Tests in `<Module>/Tests`.

- Game layer split: rules in plain C# (`Scripts/Core`), announced on the bus; views in `Scripts/View` bound to entities through `EntityViewRegistry`. Views only read state and call the game API.
- Generated assets (prefabs, scene) come from an editor builder run in batch mode (in a worktree when the Editor has the main project open):
  `Unity.exe -batchmode -quit -nographics -projectPath <project> -executeMethod ReferenceGame.Editor.GoblinValleyBuilder.Rebuild -logFile <log>`
- Docs: before any commit that touches docs or a version, run `python -I .github/scripts/check_docs.py . --local`. A
  release goes through `python .github/scripts/release.py X.Y.Z` in the release commit, together with the
  TARGET-ARCHITECTURE text, the `Docs/RELEASES.md` row and the open issues it closes (CHANGELOG names the KI).

## How to verify (Unity Editor is usually open on the main project)

Recent sessions run tests in the main project (no Editor open on it); there is no verify worktree now. If the Editor
is open, create one first: `git worktree add --detach D:/RPG-Framework-verify <sha>` (remove it with
`git worktree remove --force D:/RPG-Framework-verify`).

```bash
unity test "D:/RPG-Framework" --mode EditMode --output <xml> --verbose --timeout 1800
unity test "D:/RPG-Framework" --mode PlayMode --output <xml> --verbose --timeout 1800
python -I .github/scripts/check_meta.py .
python -I .github/scripts/check_docs.py . --local
```

## Gotchas learned

- Bash heredocs with Python triple quotes can fail to parse (`unexpected EOF`); write edit scripts to the scratchpad with the Write tool and run them with `python -I`.
- `GameArchitectureTests` (TestSupport) lists each rule as its own test; a new rule needs a test there too (`EveryRule_IsATestInEveryGame` checks).
- Don't write regex escapes (`\b`) through bash heredocs into C#; use the Edit tool.

- Batch-mode Unity exits with code 2 when any test fails. Read the NUnit XML, not the exit code.
- A test assembly whose plugin fails to load reports **0 tests, not an error**. Compare totals between runs (the expected totals are in the top row of `Docs/RELEASES.md`; the Multiplayer Services SDK must be installed).
- The verify worktree gets NuGetForUnity files generated on import; remove them there before `checkout`, or checkout aborts and the old commit is tested. Check the SHA the run actually used.
- A MonoBehaviour must live in a file of its own name (enforced by `FrameworkArchitectureTests`). Test MonoBehaviours go in a `*.TestSupport` runtime assembly.
- Test assemblies that construct `EventBus` need `RPGFramework.Logging.Runtime` (CS0012 otherwise). Runtime assemblies that call `IEventPublisher` reference both `RPGFramework.Events.Runtime` and `UniTask`.
- Parallel subagents can each wire one module (own files only, no Unity, no git); run one combined test pass afterwards.
- A `.meta` without a valid 32-hex GUID makes Unity ignore the asset, silently. CI catches this.
- Every new file and folder needs a committed `.meta`.
- Stage explicit paths only. The working tree holds user changes that must not be committed (Addressables settings, `Packages/packages-lock.json`, `ProjectSettings/SceneTemplateSettings.json`, `Заупск.txt`).
- Garbage tests under Unity Mono: `GC.GetAllocatedBytesForCurrentThread` always returns 0, and `Is.Not.AllocatingGCMemory()` drops the `Not`; use `new AllocatingGCMemoryConstraint().ApplyTo(d).IsSuccess` (true = allocated). Count over many ticks and allow at most 2 dirty: CI runners drop stray allocations. `List.Sort` allocates even with a cached comparer; `foreach` over `IList` boxes the enumerator.
- UniTask:
  - `Preserve()` does not allow several awaiters while the task is pending. Use `UniTaskCompletionSource` for shared in-flight work.
  - Tests rely on synchronous completion and check `.Status`.
- EditMode specifics:
  - `OnDestroy` fires only on `[ExecuteAlways]` behaviours.
  - Use `DestroyImmediate` when `!Application.isPlaying`.
- Avoid namespaces that shadow UnityEngine types (`Resources`, `SceneManagement`, `Time`, `Debug`). Fully qualify `UnityEngine.Time`.
- NuGetForUnity regenerates `Assets/NuGet.config` and `packages.config` on import.

- `EditorSceneManager.OpenScene(..., Single)` unloads unused assets: reload asset references created before it.
- Folders ending in `~` (`Templates~`, `Documentation~`) are not imported by Unity and need no metas.
- Large bash heredocs with apostrophes fail to parse in this environment; write files with the Write tool.
- Logging extension methods (`LogError`, ...) need `using RPGFramework.Logging;`.
- Unity Transport on Windows logs an error ("All socket receive requests were marked as failed") when it sends to a localhost port nobody listens on; tests of an unreachable address use 192.0.2.1 (TEST-NET-1) instead.
- A co-op test that moves a friend's view must walk it (a little each frame): a jump is corrected by the host's speed check.
- Co-op screenshots: a temporary `Assets/_Shot` PlayMode test in the verify worktree hosts the Goblin Valley scene and adds a second `ActionRpgHost` that joins over 127.0.0.1 (its asmdef needs `RPGFramework.Bootstrap.Unity`). Delete it afterwards.

## Decisions waiting for the user

- KI-05: the live Unity Relay test needs the owner's Unity Gaming Services project linked in Project Settings > Services.
- 1.0: tagged only on the owner's explicit yes.
