# Handoff for Next Session

**Date:** 2026-10-27
**Project:** RPG Framework (`D:\RPG-Framework`, https://github.com/Aizekhan/RPG-Framework)
**Read first:** `STATUS.md` (verified gates, migration table, open issues), then `Docs/archive/ARCHITECTURE-GAP-ANALYSIS.md` §6 (migration plan)

---

## Where things stand

- Last code-verified SHA: **`41e990e`**: compile 0 errors, **1696/1696 EditMode**, **10/10 PlayMode** (Multiplayer Services SDK 2.4.1 installed in the main project), meta check clean, CI green (run `40438078474`).
- **Released `v0.2.0`** (tag on `01f5af8`). **Do not release v0.3.0 yet**: the user wants the whole plan finished first (reliability, weak spots, PvP).
- **Unity Relay** (steps 1-3 done): `9d08fd4` opened `UnityNetworkTransport` for relays; `451afe3` added the optional module `Networking/UnityRelay` (asmdef define `RPG_MULTIPLAYER_SERVICES`, self-registers in `NetworkSessionProviders`, F2 panel toolbar, `-provider unity-relay`). Proven with a fake cloud only, and by a clean install with and without the SDK (`D:/RPG-Release020`). **Open gate:** live test across two networks with the owner's UGS project; check Unity's current Relay terms before calling it free.
- **Claude Code starter** (`ed1aaf7`): `START-NEW-GAME.md` at the repo root (README links it). The developer drops it into an empty folder and Claude Code follows steps 0-10. Run end to end in `D:/RPG-Starter-Test`. New rules GA11/GA12 plus `GAME.md` / `FRAMEWORK-GAPS.md` / `DEVLOG.md` stop an assistant from rebuilding framework modules or hiding gaps. Selling only the file is not possible while the repo is public MIT (licence is the owner's decision, near 1.0).
- **Co-op stage 3 done** (`555f079`, 1322/1322 EditMode, 9/9 PlayMode): timeouts (10 s), unstable indicator (2 s), seat kept 2 min, automatic reconnect (3 tries), bad-message limit, host autosave. Details in `networking.md` "How the action RPG co-op works" 6-8.
- **Saves done** (`5dcc822`): section versions + `Upgrade`, previous-save fallback, unreadable saves set aside, newer-build saves protected, JSON serializer rewritten (old saves still load), recipe 12.
- **Weak spots done** (`9182fda` `StatContainer`, `c685e43` DeltaCompression + Tools fixes, `da76940` 45 dead events published, `df0e768` Skills storage, loot weights, 1000-entity benchmarks). Dead custom inspectors replaced in `baa8dde`.
- **PvP done** (`ba094c9`): `ActionRpg/Runtime/Pvp` (`IHostilityRules`, `TeamHostility`, `FactionHostility`, `PvpSettings`, `PvpMatch`, `PvpStatus`, events), co-op protocol v4 (teams + match in the world message, `CoopCorrectionMessage`, host speed check, `CoopHost.Teleport`), Unity `PvpSetup` in Game Setup, HUD score/banner, side colours. Recipe 13. Movement check is horizontal only.
- **Designer assets done** (`baa8dde`): `Tools/{Dialogue,Faction,Skill,Ability,Crafting,StatusEffect}Editor` (asset + database + inspector + tests each), Create menu updated, the 9 dead `Editor/CustomEditors` removed. Crafting fix: crafts now finish (`UpdateCrafting` was empty), optional `ICraftingInventory`.
- **Runtime gaps fixed** (`41e990e`): Dialogue conditions/branches/events (`DialogueManager(..., events, contextProvider)`), Abilities rules (`CompleteCast`/`Update(time)`, toggles, channels, percentage costs), StatusEffects modifiers via `IStatusEffectTarget` + `Integration/StatusEffectsStats`, Crafting `ICraftingRequirementChecker`, Skills duplicates/prerequisites, Factions `Involves`. Smaller leftovers are in STATUS.md Open issues.
- **Next, in order:** a second genre sample → 1.0. See STATUS.md "Next steps".
- User's co-op plan (2026-10-26): stage 1 names and colours (done, `8948d16`); stage 2 play between homes: providers architecture done (`11c9de6`, `INetworkSessionProvider`), service choice researched in `networking.md`, waiting for the user (Unity Relay recommended, needs their UGS project); stage 3 reliability: not started. Principle from the user: network core never depends on a provider; each service is a separate module.
- **Co-op works** (user approved 2-4 players): any `ActionRpgHost` game, F2 > Host a game / Join an IP, or `-host` / `-join <ip>`. Host-authoritative: the host's `ActorWorld` runs the rules with everyone in one party; joined machines restart into a core-only `GameHost` and mirror the world (`RemoteWorld`). Code: `ActionRpg/Runtime/Coop`, `ActionRpg/Unity/{ActionRpgHost,CoopPanel,RemoteMotion,ActorPresenter}.cs`, `Networking/UnityTransport`. Docs: `Documentation~/networking.md`, recipe 11.
- Goblin Valley (`Assets/ReferenceGame`) is now an `ActionRpg` game: assets in `Content/`, one coded rule (`KillStreak`), rebuilt by `GoblinValleyBuilder` in batch mode.
- The user story is delivered: create a Unity project, add the package, **RPG Framework > Game Kit > New Game**, Play: a hero walks and fights goblin waves with a HUD, character sheet (I/Tab) and saved progress. New enemies/items/quests are assets (`Character`, `Game Setup`, Item, Loot Table, Quest Asset); code only for unique rules (`ActionRpgHost.ConfigureGameModules`, events on the bus).
- Proven in a fresh project `D:/RPG-Fresh` (manifest points at the package in `D:/RPG-Framework-verify`, so its contents follow that worktree).
- Leftovers of an old New Game run (`Assets/SampleQuest`, `Docs/`, `.github/workflows/unity-tests.yml`, a CLAUDE.md block) were removed from the main tree at the user's request; a copy is in the session scratchpad `leftovers-backup`.

## What was done

1. Technical audit, plus fixes for every finding (see STATUS.md, "Fixed in the audit").
2. `Docs/archive/ARCHITECTURE-GAP-ANALYSIS.md` (`3fb53cb`) compared MythHunter's core with the framework.
3. Migration phases 1–7 (see the STATUS.md table): Logging, Core/UniTask, Events, Systems/World, Bootstrap/GameHost, ECS pools and archetypes, Pooling, AssetManagement, Scenes, Timers, AsyncStateMachine.
4. Phase 8: `UINavigation` module. Phase 9: loot builder, save coordinator (+ load data-loss fix), network authority, diagnostics, DI optional parameters, module events, Combat→Quests bridge.
5. Product milestones 1-6 (STATUS.md): UPM package, Game Kit, action RPG from the inspector, Goblin Valley on it, co-op.

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

- `GameArchitectureTests` (TestSupport) lists each rule as its own test; a new rule needs a test there too (`EveryRule_IsATestInEveryGame` checks).
- Don't write regex escapes (`\b`) through bash heredocs into C#; use the Edit tool.

- Batch-mode Unity exits with code 2 when any test fails. Read the NUnit XML, not the exit code.
- A test assembly whose plugin fails to load reports **0 tests, not an error**. Compare totals between runs (expected: 1696 EditMode, 10 PlayMode, with the Multiplayer Services SDK installed).
- The verify worktree gets NuGetForUnity files generated on import; remove them there before `checkout`, or checkout aborts and the old commit is tested. Check the SHA the run actually used.
- A MonoBehaviour must live in a file of its own name (enforced by `FrameworkArchitectureTests`). Test MonoBehaviours go in a `*.TestSupport` runtime assembly.
- Test assemblies that construct `EventBus` need `RPGFramework.Logging.Runtime` (CS0012 otherwise). Runtime assemblies that call `IEventPublisher` reference both `RPGFramework.Events.Runtime` and `UniTask`.
- Parallel subagents can each wire one module (own files only, no Unity, no git); run one combined test pass afterwards.
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
- Unity Transport on Windows logs an error ("All socket receive requests were marked as failed") when it sends to a localhost port nobody listens on; tests of an unreachable address use 192.0.2.1 (TEST-NET-1) instead.
- A co-op test that moves a friend's view must walk it (a little each frame): a jump is corrected by the host's speed check.
- Co-op screenshots: a temporary `Assets/_Shot` PlayMode test in the verify worktree hosts the Goblin Valley scene and adds a second `ActionRpgHost` that joins over 127.0.0.1 (its asmdef needs `RPGFramework.Bootstrap.Unity`). Delete it afterwards.

## Decisions waiting for the user

- Which internet provider first (Unity Relay recommended; Steam / EOS later as separate modules)? Unity Relay needs a Unity Gaming Services project linked in Project Settings > Services.
- Stage 3 (reliability) after the provider, or before?
