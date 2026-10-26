# RPG Framework - Project Status

**Last Updated:** 2026-10-26
**Repository:** https://github.com/Aizekhan/RPG-Framework (public, MIT)
**Branch:** main (force-push and deletion blocked)
**Unity:** 6000.0.45f1 · Unity CLI 1.0.0-beta.14

---

## Verified state

| Gate | Result | SHA |
|---|---|---|
| Clean-checkout compile (`unity test`, fresh worktree) | ✅ 0 `error CS` | `03d28e0` |
| EditMode tests | ✅ **888 / 888 passed** | `03d28e0` |
| `.meta` integrity (CI `meta-check`) | ✅ 0 problems | `7c3cc2d` |
| CI Unity tests on GitHub | ⚠️ **skipped**: secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` not set | — |

Commits after `03d28e0` touch only `.github/`, `Core/Installers/.gitkeep` and docs.

Earlier "~5 compilation errors" and "100% pass rate" claims were unverified. In reality HEAD `e348e10` did not compile from a clean clone, and ~150 tests (Guilds, Quests, Dialogue, Crafting) were silently not running.

## Codebase (committed, from `git ls-files`)

- **36 module folders** in `Assets/_Framework`: Abilities, Achievements, BehaviorTree, Characters, Combat, Companions, Core, Crafting, DI, DeltaCompression, Dialogue, DynamicEvents, ECS, Editor, Environment, Factions, Guilds, Housing, Inventory, Items, Logging, Loot, Networking, Pathfinding, Persistence, Pooling, Progression, Quests, Serialization, Skills, StateMachine, Stats, StatusEffects, Tools, Trading, UI
- **73 asmdef**, **621 .cs files**, **~61,200 C# lines**
- No AI, Economy or Objectives modules exist (older docs listed them).

## Uncommitted work in the local tree (not verified, not in git)

- EPIC 25 modules: Camera, Debug, Resources, SceneManagement, Systems, Time; empty `Audio/`
- Pooling: `GameObjectPool.cs`, `PoolManager.cs`, asmdef `noEngineReferences: false`
- `Assets/ReferenceGame/` (Goblin Valley sample; depends only on Characters + Stats)
- NuGetForUnity-generated `Assets/NuGet.config`, `packages.config`, `Packages.meta`
- `ProjectSettings.asset`: `activeInputHandler` 0 → 2 (Both)
- `COMPLETE-FRAMEWORK-ARCHITECTURE.md`, `FRAMEWORK-ARCHITECTURE-MAP.md` (contain stale counts)

## Fixed in this session (2026-10-26)

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

## Open issues (from the 2026-10-26 audit)

**High**
- Duplicate types: `IStatusEffect`/`StatusEffectManager` (Combat vs StatusEffects), `QuestReward`/`QuestObjective` (Quests vs Tools/QuestEditor), `StatModifier` (Stats vs ItemAsset), `EquipmentSlot` (class in Inventory, enum in UI)
- UI panels are stubs; `InventoryPanel` takes `object`; 25 `AddListener` with no `RemoveListener`; 3 event subscriptions in `EquipmentPanel` never removed

**Medium**
- Test asmdef hygiene: DI, ECS, Logging lack `UNITY_INCLUDE_TESTS` and `nunit.framework.dll`; Environment lacks the constraint
- `Packages/manifest.json` has both `com.unity.textmeshpro 3.2.0-pre.10` and `com.unity.ugui 3.0.0` (TMP is part of ugui in Unity 6)
- `StatContainer`: LINQ + boxing on every recalculation; `Convert.ChangeType` banker's rounding for int stats
- Uncommitted `PoolManager`/`SystemRegistry` singletons: `FindObjectOfType` (deprecated), `DontDestroyOnLoad` (throws in EditMode), static instance never reset
- Uncommitted namespaces `RPGFramework.Time/Debug/Camera/Resources` will shadow `UnityEngine.Time/Debug/...` in referencing assemblies
- `ECS/Tests/ArchitectureTests.cs` hardcodes `MythHunter.GameLogic` and checks only the ECS assembly
- 3 Editor windows wrapped in `#if FALSE` (Quest, DialogueGraph, FactionRelationshipMap)
- `Core/Installers` is an empty folder (kept with `.gitkeep`); decide whether to remove

**Low**
- Modules without tests: Core, Characters, DeltaCompression, Editor, Tools, UI

## Next steps

1. Add Unity license secrets so CI runs the EditMode suite.
2. Decide and commit (or discard) the uncommitted EPIC 25 modules, Pooling, ReferenceGame, NuGet files, `activeInputHandler`.
3. Resolve duplicate types.
4. Test asmdef hygiene + TextMeshPro/ugui package conflict.
