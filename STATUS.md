# RPG Framework - Project Status

**Last Updated:** 2026-10-26
**Repository:** https://github.com/Aizekhan/RPG-Framework (public, MIT)
**Branch:** main (force-push and deletion blocked)
**Unity:** 6000.0.45f1 · Unity CLI 1.0.0-beta.14

---

## Verified state

| Gate | Result | SHA |
|---|---|---|
| Clean-checkout compile (`unity test`, verify worktree) | ✅ 0 `error CS` | `d345bf6` |
| EditMode tests | ✅ **1285 / 1285 passed** (incl. co-op over `LoopbackNetwork` with 8 players, session providers, real UDP) | `d345bf6` |
| PlayMode tests (Goblin Valley scene, assets-only action RPG, save/reload, **co-op: two hosts over UDP, eight games over UDP**, unreachable address) | ✅ **7 / 7 passed** | `d345bf6` |
| `.meta` integrity | ✅ 0 problems | `d345bf6` |
| CI on GitHub (meta check, EditMode, PlayMode) | ✅ green (run `40389083150`) | `d345bf6` |
| Fresh Unity 6 project + UniTask + package → New Game → tests → Play | ✅ 14/14 generated tests, played (screenshots) | `675b521` |
| **Release `v0.2.0`**: fresh Unity 6 project installs the package by git URL `#v0.2.0` (resolves to `01f5af8`), 0 `error CS`, New Game → 14/14 generated tests | ✅ CI green (run `40386565078`) | `01f5af8` |

EditMode went from 1250 to 1243 (before networking) because Goblin Valley's own wave and rule tests were replaced by tests of its
setup asset and kill streak rule (waves are covered by the `ActionRpg` tests).

Earlier "~5 compilation errors" and "100% pass rate" claims were unverified. In reality HEAD `e348e10` did not compile from a clean clone, and ~150 tests (Guilds, Quests, Dialogue, Crafting) were silently not running.

## Codebase (committed, from `git ls-files`)

- The framework is the embedded UPM package `Packages/com.aizekhan.rpg-framework` (moved from `Assets/_Framework` in `ad978e6`, GUIDs kept); install in other projects by git URL (see its README).
- **47 module folders** (Presentation added in `de2498e`). Added since the audit: AssetManagement, Bootstrap, Cameras, Diagnostics, Events, Integration, Scenes, Spawning, Systems, Timers, UINavigation
- **106 asmdef**, **718 .cs files**, **~76,500 C# lines**
- `Assets/ReferenceGame`: sample game (Goblin Valley) built only on framework assemblies
- `Assets/_Tests/Editor/FrameworkArchitectureTests.cs`: framework-wide rules (no game references, no editor code in player builds, no assembly cycles, `RPGFramework.*` namespaces, every MonoBehaviour/ScriptableObject in a file of its own name)
- No AI, Economy or Objectives modules exist (older docs listed them).

## Core migration from MythHunter (`Docs/archive/ARCHITECTURE-GAP-ANALYSIS.md` §6, approved 2026-10-26)

Each row was verified on a fresh worktree at the SHA shown (0 `error CS`, all EditMode tests green), then pushed.

| Phase | What landed | SHA | Tests |
|---|---|---|---|
| 1 Logging | `Logger` (per-category levels, context, enrichers, isolated sinks), Memory/File (rotation) sinks, `Logging.Unity` console sink | `0ff5f6e` | 904 |
| 2 Core | UniTask 2.5.10, `InitializationTransaction`, `AsyncTaskTracker`, `Ensure` guards, DI `Validate()` (missing deps, cycles) | `297e82c`, `6c05543` | 934 |
| 3 Events | `EventBus` (priorities, sticky, queue + bounded `Flush`, async priority groups, recursion and duplicate guards), `EventSubscriberBase`/`SubscriptionBag`, throttler, batcher, recorder | `3e0d49e`, `4b1ebcb` | 974 |
| 4 Systems | Pure C# `SystemGroup`/`SystemRegistry` (Update/Fixed/Late stages, priorities, error isolation, phase filtering), `SystemBase`, `PhaseTracker`, `World` (successor of MythHunter `EcsWorld`) | `54103de` | 1005 |
| 5 Bootstrap | DI `RegisterFactory`; composition with validation + rollback; `FrameworkCoreModule`, `GameFeatureModule`, `GameHost`, `GameHostBehaviour` | `349c761` | 1020 |
| 6 ECS | Typed component pools (no boxing, `GetComponentRef`), multi-component queries, lifecycle events, archetypes (`ArchetypeCatalog`); replaces MythHunter's stale-prone `ComponentCache` | `21afa2e` | 1038 |
| 7a Pooling | Leak tracking, double/foreign release protection, `PoolManager` with owners, `PoolMonitor`, `Pooling.Unity` `GameObjectPool` | `c058fd3` | 1054 |
| 7b Assets | Ref-counted async `AssetManager` with providers and shared in-flight loads, `ResourcesAssetProvider`, `InstantiateAsync` | `ba479c9`, `71a1f83` | 1066 |
| 7c Scenes | Serialized `SceneDispatcher` (transitions, typed payloads, events), `UnitySceneBackend` | `6636329` | 1074 |
| 7d Timers | `TimerService` (handles, categories, TimeScale, `DelayAsync`), ticked by the core module | `1ae1c21` | 1087 |
| 7e StateMachine | `AsyncStateMachine` (serialized transitions, rules, payloads) | `bbdb168`, `8fc25fc` | 1097 |
| 7f Core async | `AsyncSerialQueue`; `SceneDispatcher` and `AsyncStateMachine` now built on it | `199c144` | 1102 |
| 8 UINavigation | `NavigationService` (string ids, serialized operations, history, modals with typed results, scene roots), transition rules, `SceneNavigationHandler`; Unity: `NavigableView`, `ModalView<T>`, `ViewConfig`, `PrefabViewFactory` (layers, reuse, preload), `CanvasGroupTransition` | `77b5433`, `7882e4b`, `e904270` | 1127 |
| 9a Loot | Validating `LootTableBuilder` | `da7b553` | 1131 |
| 9b Persistence | **Bug fix:** `SaveSystem.Load` deserialized state as `object` and lost all data; typed `Load<T>`/`QuickLoad<T>`. `SaveGameCoordinator` with per-system `ISaveParticipant` sections | `04235ff`, `f519967` | 1140 |
| 9c Networking | Several handlers per message (isolated errors, `IDisposable` registration), `MessageAuthority` (Server/Client/Any) with rejection stats | `25ac1c9`, `0c59369` | 1146 |
| 9d Diagnostics | `SystemProfiler` hook in `SystemGroup`, event type statistics; new `Diagnostics` module: `DiagnosticsHub`, `PerformanceMonitor`, sections (performance, systems, events, pools, timers), `DiagnosticsModule`, IMGUI `DiagnosticsOverlay` | `6e41eaf`, `9dd2975` | 1156 |
| 9e DI | Optional constructor parameters: injected when registered, default value otherwise; validation skips them | `b7af7f9` | 1157 |
| 9f Module events | Gameplay modules publish their events through an optional `IEventPublisher`: Quests (+ `FailQuest`, `ReportProgress`), Progression, Abilities (+ `CooldownTracker.CollectExpired`), Guilds, Inventory. Event types implement `IEvent` | `7427114`…`de95bd9`, `7e68d3a`, `d82835c`, `35a1dbb` | 1170 |
| 9g Module bridge | `RPGFramework.Integration.CombatQuests`: `KillObjectiveBridge` turns `ILifeStateManager` deaths into Kill-objective progress (game supplies target ids and kill credit) | `dee45d1`, `25bb6a1` | 1163 |

**Migration plan (`Docs/archive/ARCHITECTURE-GAP-ANALYSIS.md` §6) is complete.** Everything above is pushed to `origin/main`.

Not ported on purpose in phase 9: a persistent event store (the `EventRecorder` ring buffer covers debugging; no proven need for event sourcing).

**Not done on purpose:**
- Addressables 2.4.6 installed; `AssetManagement.Addressables` (`AddressablesAssetProvider`, compiled only when the package is present) has no automated test yet. Default Addressables settings (`Assets/AddressableAssetsData`, one empty Default Local Group) are committed; nothing is addressable yet, prefabs are referenced directly through `PrefabCatalog`.
- `ParallelSystemRegistry` not ported. Unity API is main-thread only and there is no proven need.

## Product milestones (2026-10-26)

| # | Milestone | SHA | Proof |
|---|---|---|---|
| 1 | Framework shipped as UPM package `com.aizekhan.rpg-framework` 0.1.0 (README, CHANGELOG, LICENSE; meta check covers embedded packages) | `ad978e6` | 1184/1184 EditMode |
| 3 | **Game Kit (AI Kit).** Game architecture rules GA01–GA10 (`GameArchitectureRules` over `GameCodebase`) with a `GameArchitectureTests` base fixture; New Game / New Feature generators (`Templates~`, menu window + batch mode) producing Core/runtime/test assemblies, composition, host, scene and tests with metas; `AIGuidance` installs a CLAUDE.md block, `Docs/RPGFramework` guide + 9 recipes and a CI workflow. ReferenceGame rules moved to `ReferenceGame.Core` (noEngineReferences) and pass the rules | `6335b9d` | 1228/1228 EditMode, 1/1 PlayMode. Also proven in the verify worktree: generated `SampleQuest` + `Shop` feature compile and pass 16 tests (1244 total); injected singleton, `FindObjectOfType` and engine-enabled Core fail GA06, GA05, GA03 with fix hints |
| 4 | **Action RPG from the inspector.** `ActionRpg` module: `Character` and `Game Setup` assets (player, enemies, waves, items, loot, quests, validation in plain words), engine-free `ActorWorld`/`ActionRpgModule`, `ActionRpgHost` that runs any prefab (controls, AI, strike, health bar added when missing; placeholder capsule without a prefab), camera follow, HUD with character sheet/backpack (I/Tab), progress saved between sessions. Game Kit **New Game** now produces a playable game with starter content | `bbaa32d`, `fcde87f`, `5136445`, `675b521`, `ef8beeb` | 1250/1250 EditMode, 4/4 PlayMode; generated game 14/14; fresh-project install proven |
| 5 | **Goblin Valley on `ActionRpg`.** The reference game is built like a user's game: Game Setup + Character assets (Hero, Goblin, new Goblin Brute), items/loot/quests as assets, model prefabs, one coded rule (`KillStreak`). Its own presenter, HUD, controller, AI, waves and C# content were deleted (approved by the user). Plus: item icons in the backpack, F3 diagnostics, mesh health bars tinted | `46b8481`, `6eca982` | 1243/1243 EditMode, 4/4 PlayMode, screenshots of the valley and the backpack |
| 6 | **Co-op for 2-4 players.** Real transport `UnityNetworkTransport` (UDP, Unity Transport 2.5.3) + `LoopbackNetwork`; `ActorWorld` party; engine-free `CoopHost` / `CoopClient` / `RemoteWorld` (host-authoritative, per-player diffs, fingerprint check); `ActionRpgHost` F2 panel, `-host` / `-join <ip>`, `RemoteMotion`, `GameHostBehaviour.Restart`. Recipe 11, `networking.md` | `5c69af2`, `ade7c21`, `1bef39e`, `11c9de6` | 1266/1266 EditMode, 6/6 PlayMode (two hosts over localhost UDP), screenshot of the friend's screen |
| 7 | **Co-op names, session providers, up to 8 players.** Names above heads and party colours; `INetworkSessionProvider` (LAN, loopback; the core never depends on a service); release `v0.2.0`; `ActionRpgHost` **Max Players** 2-8 with generated colours past the palette, friends spawn in a ring; fix: a join before the game started fell back to solo. Unity Relay: compatibility checked, adaptation plan in `networking.md`, not integrated | `8948d16`, `11c9de6`, `01f5af8`, `d345bf6` | 1285/1285 EditMode, 7/7 PlayMode (eight games over UDP) |
| 2 | **Playable 3D Goblin Valley.** Framework `Presentation` module (`EntityViewRegistry`). Game: spawn/despawn/wave events, `GoblinWaveSystem`, inspector-tunable `GoblinValleyConfig` + `PrefabCatalog`, presenter spawning prefabs through `PrefabSpawner` (goblins pooled), `HeroController` (Input System: keyboard, mouse, gamepad), `GoblinBrain`, health bars, UGUI HUD, `CameraFollow`. `GoblinValleyBuilder` generates placeholder art, prefabs, config and scene (menu or batch mode). CI runs EditMode + PlayMode | `de2498e` | 1196/1196 EditMode, 1/1 PlayMode |

Roadmap, in order (agreed with the user 2026-10-26):
3. ~~AI Kit~~ done (`6335b9d`).
4. ~~Content as ScriptableObjects~~ done (`ActionRpg`, milestone 4).
5. ~~New-game template~~ done: New Game creates a playable game; install in a fresh project proven.

## Cleanup after the migration (2026-10-26)

| SHA | Change |
|---|---|
| `0c936a9` | **Persistence bug:** metadata lived only in memory and the checksum was never stored. Now a JSON sidecar (`slot.info`) holds both; metadata survives restarts, tampered saves fail with `InvalidChecksum`, writes go through a temp file |
| `24274c0` | Duplicate types removed: Combat's own status-effect system (StatusEffects is the single module); Tools `QuestObjectiveEntry`/`QuestRewardEntry`/`ItemStatModifierEntry`; UI `EquipmentSlotType` |
| `55db5f2` | **UI bug:** `EquipmentSlotUI`, `AbilitySlotUI`, `AbilitySlotHUD` were declared in other classes' files, so Unity could not attach them to any prefab. Own files now (`ItemSlotUI` is the general slot). `InventoryPanel` takes `IInventoryContainer<IItem>` instead of `object` |
| `162efbf`, `91aee01` | Framework-wide architecture tests; test asmdef hygiene (DI, ECS, Logging, Environment); `com.unity.textmeshpro` stub removed, `ugui` pinned to the 2.0.0 Unity resolves |
| `fd6da39`, `ccbf327`, `7671fb8` | Bus integration: Combat `DeathEvent`/`ResurrectionEvent` published on the bus; `StatGrowthPublisher` (level-up → `StatIncreasedEvent`); `GuildTreasury` publishes deposits/withdrawals; `ILifeStateManager` exposes entity registration. `Bootstrap.Gameplay` feature modules (Combat, Progression, Quests, Items, Loot, CombatQuests). ReferenceGame rebuilt as Goblin Valley: composed in `GameHost`, every rule a bus subscription, end-to-end tests (only `7671fb8` compiles; the two before it do not) |
| `a968867`, `ff70d11` | Addressables provider + package; `Spawning` (`PrefabSpawner`: spawn by key, optional pooling/prewarm/release; `PrefabCatalog`); legacy `UISystem`/`IView`/`ViewId` enum removed (duplicated UINavigation, unused): `UIViewBase` derives from `NavigableView`, `Close()` goes back through navigation; UI and Spawning tests |
| `a60ab53`, `e6b70cc` | EPIC 25 leftovers: `Cameras` (CameraFollow), `Timers.Unity` (`TimeScaleController`: pause, slow motion), `DebugDraw` in `Diagnostics.Unity`; Characters tests; ReferenceGame; `activeInputHandler` Both; NuGetForUnity config; empty `Core/Installers` removed |

Not committed (they duplicated migrated modules; the backup folder was deleted on 2026-10-26 with the user's approval): EPIC 25 `Systems/`, Pooling `PoolManager`/`GameObjectPool`, Debug logger (→ Logging), `Resources` (→ AssetManagement), `SceneManagement` (→ Scenes), empty `Audio`, and the stale `COMPLETE-FRAMEWORK-ARCHITECTURE.md` / `FRAMEWORK-ARCHITECTURE-MAP.md`.

## Fixed in the audit (2026-10-26)

| SHA | Fix |
|---|---|
| `113dd48` | Restored Tools runtime assemblies dropped by `e348e10`; kept original GUIDs; replaced fake QuestEditor GUIDs |
| `185d03f` | Characters module (`ICharacter`, `CharacterBase`), `IStats`, UI panels wired |
| `0a77347`, `1d85c34` | Guilds test assets had no GUIDs (51 tests never ran); test updated to current `GuildManager` ctor |
| `584fca5` | Bug: `DisbandGuild` left the leader in the guild |
| `a958c55` | Moq failed to load (`Castle.Core` → `System.Diagnostics.EventLog`); 100 tests silently skipped |
| `28521c7` | Bug: `TimeSystem` ignored start hour (12:00 + 1h = 01:00); bug: `EventParticipant` leaving at t=0 stayed active, Observers were reward-eligible |
| `03d28e0` | **Security:** `BinaryFormatter` removed from `BinarySaveSerializer` (now header + GZip JSON). **Bug:** JSON saves lost decimals under comma-decimal locales (85.5 → 85.0, proven by failing test on old code). Breaking: old binary saves unreadable |
| `b9d96b5`, `7c3cc2d` | GitHub Actions CI: `.meta` integrity + GameCI EditMode tests |

## Backlog from archived plans

Checked before moving the old plans to `Docs/archive/` (2026-10-26). Ideas that are still open and not listed elsewhere here; none is scheduled:
- **Performance benchmarks** (CLAUDE.md validation step 4; old KPI: < 16 ms frame with 1000+ entities). None exist yet.
- **Editor tools:** dialogue graph, skill/ability tree, faction relationship map (the three `#if FALSE` windows below), a combat balancing simulator.
- **Bigger features:** procedural generation (dungeons, quests), tactical / boss AI, save versioning and migration, cloud saves.
- **Release extras:** API reference, tutorials, more sample games of other RPG types, CONTRIBUTING and issue templates, Asset Store submission (licence decision is the owner's).

## Open issues

**Medium**
- UI panels are thin defaults (EquipmentPanel hard-codes 8 slot types; game layers override). The earlier "listeners never removed" note was wrong: they are on the panel's own child buttons and die with it
- `StatContainer`: LINQ + boxing on every recalculation; `Convert.ChangeType` banker's rounding for int stats
- 3 Editor windows wrapped in `#if FALSE` (Quest, DialogueGraph, FactionRelationshipMap)
- `ICharacter.Inventory`/`Progression` are `object` (keeps Characters free of those modules)

**Low**
- Modules without tests: DeltaCompression, Editor, Tools
- Loot `LootGeneratedEvent`/`LootDroppedEvent` are not `IEvent`s and nothing raises them
- ReferenceGame art is generated placeholders (primitives)
- Co-op is LAN/VPN only: no Unity Relay or NAT punch-through yet; positions are trusted from each player (fine among friends). See `Documentation~/networking.md`
- CI: the license file is bound to another machine, so GameCI falls back to email/password activation (works)

## Next steps

1. Co-op over the internet: a Unity Relay session provider (needs the owner's Unity Gaming Services project). Then stage 3: reliability (client drop, host leaving, reconnect, malformed messages, what happens to saves when the host quits).
2. Tests for Tools (and DeltaCompression); loot events as bus events (or remove them); re-enable or delete the three `#if FALSE` editor windows.
3. `StatContainer` performance and int rounding, after a benchmark.
