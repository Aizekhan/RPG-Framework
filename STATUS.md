# RPG Framework - Project Status

**Last Updated:** 2026-10-26
**Repository:** https://github.com/Aizekhan/RPG-Framework (public, MIT)
**Branch:** main (force-push and deletion blocked)
**Unity:** 6000.0.45f1 · Unity CLI 1.0.0-beta.14

---

## Verified state

| Gate | Result | SHA |
|---|---|---|
| Clean-checkout compile (`unity test`, fresh worktree) | ✅ 0 `error CS` | `35a1dbb` |
| EditMode tests | ✅ **1170 / 1170 passed** | `35a1dbb` |
| `.meta` integrity (CI `meta-check`) | ✅ green on every push | `35a1dbb` |
| CI Unity tests on GitHub (GameCI) | ✅ **1170 / 1170 passed**, run `40337490738` | `ebaa00e` + CI fix |

Earlier "~5 compilation errors" and "100% pass rate" claims were unverified. In reality HEAD `e348e10` did not compile from a clean clone, and ~150 tests (Guilds, Quests, Dialogue, Crafting) were silently not running.

## Codebase (committed, from `git ls-files`)

- **45 module folders** in `Assets/_Framework`. Added since the audit: AssetManagement, Bootstrap, Diagnostics, Events, Integration, Scenes, Systems, Timers, UINavigation
- **100 asmdef**, **709 .cs files**, **~76,500 C# lines**
- No AI, Economy or Objectives modules exist (older docs listed them).

## Core migration from MythHunter (`ARCHITECTURE-GAP-ANALYSIS.md` §6, approved 2026-10-26)

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

**Migration plan (`ARCHITECTURE-GAP-ANALYSIS.md` §6) is complete.** Everything above is pushed to `origin/main`.

Not ported on purpose in phase 9: a persistent event store (the `EventRecorder` ring buffer covers debugging; no proven need for event sourcing).

**Not done on purpose:**
- No Addressables provider. The package is not installed, so it could not be compiled or verified. `IAssetProvider` is the extension point.
- `ParallelSystemRegistry` not ported. Unity API is main-thread only and there is no proven need.

## Uncommitted work in the local tree (not verified, not in git)

- EPIC 25 modules: Camera, Debug, Resources, SceneManagement, Time; empty `Audio/`
- EPIC 25 drafts **moved (not deleted)** to `D:\RPG-Framework-EPIC25-backup\Assets\_Framework\`, because the migrated modules replace them and reuse their assembly/type names: `Systems/` (MonoBehaviour `SystemRegistry`), `Pooling/Runtime/PoolManager.cs`, `Pooling/Runtime/GameObjectPool.cs`
- Pooling runtime asmdef `noEngineReferences: false` (local edit, left untouched; the committed asmdef stays engine-free)
- `Assets/ReferenceGame/` (Goblin Valley sample; depends only on Characters + Stats)
- NuGetForUnity-generated `Assets/NuGet.config`, `packages.config`, `Packages.meta`
- `ProjectSettings.asset`: `activeInputHandler` 0 → 2 (Both)
- `COMPLETE-FRAMEWORK-ARCHITECTURE.md`, `FRAMEWORK-ARCHITECTURE-MAP.md` (contain stale counts)

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

## Open issues

**High**
- Duplicate types: `IStatusEffect`/`StatusEffectManager` (Combat vs StatusEffects), `QuestReward`/`QuestObjective` (Quests vs Tools/QuestEditor), `StatModifier` (Stats vs ItemAsset), `EquipmentSlot` (class in Inventory, enum in UI)
- UI panels are stubs; `InventoryPanel` takes `object`; 25 `AddListener` with no `RemoveListener`; 3 event subscriptions in `EquipmentPanel` never removed
- Persistence: save metadata is kept only in memory (lost after restart), and the checksum is never stored, so the integrity check always passes
- Events still without a producer: `StatIncreasedEvent` (Progression), `TreasuryDeposit/WithdrawEvent` (Guilds treasury has no publisher), Combat `DeathEvent` is a C# event on `ILifeStateManager`, not a bus event

**Medium**
- Test asmdef hygiene: DI, ECS, Logging lack `UNITY_INCLUDE_TESTS` and `nunit.framework.dll`; Environment lacks the constraint
- `Packages/manifest.json` has both `com.unity.textmeshpro 3.2.0-pre.10` and `com.unity.ugui 3.0.0` (TMP is part of ugui in Unity 6)
- `StatContainer`: LINQ + boxing on every recalculation; `Convert.ChangeType` banker's rounding for int stats
- Uncommitted namespaces `RPGFramework.Time/Debug/Camera/Resources` will shadow `UnityEngine.Time/Debug/...` in referencing assemblies (committed modules use `AssetManagement`, `Scenes`, `Timers` instead)
- `ECS/Tests/ArchitectureTests.cs` hardcodes `MythHunter.GameLogic` and checks only the ECS assembly
- 3 Editor windows wrapped in `#if FALSE` (Quest, DialogueGraph, FactionRelationshipMap)
- `Core/Installers` is an empty folder (kept with `.gitkeep`); decide whether to remove

**Low**
- Modules without tests: Characters, DeltaCompression, Editor, Tools, UI

## Next steps

1. Persistence: persist save metadata and the checksum (open issue above).
2. More bridges where games need them (e.g. Inventory→Quests Collect objectives through `ReportProgress`).
3. CI: the license file is bound to another machine, so GameCI falls back to email/password activation (works; noted for when it stops working).
4. Decide and commit (or discard) the remaining uncommitted EPIC 25 modules, ReferenceGame, NuGet files, `activeInputHandler`.
5. Resolve duplicate types.
