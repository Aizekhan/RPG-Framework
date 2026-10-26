# Changelog

## [Unreleased]

- `ActionRpg`: character and backpack panel in the HUD (I / Tab / gamepad Select); `ActorKilledEvent`, public `ActorWorld.GiveItem`, `Announce` and `ExperienceForNextLevel` for game rules
- Editor menus are all under **RPG Framework** (the tool windows moved from Tools > RPG Framework to RPG Framework > Tools)
- Install: the package depends on `com.unity.test-framework` and `com.unity.inputsystem`, and `GameKit.TestSupport` compiles without the package being testable, so a new game's tests build in a fresh project (verified: empty Unity 6 project + UniTask + this package, New Game, 14/14 tests, Play)
- `GameKit`: New Game creates a playable action RPG: starter content (hero, goblin, items, loot table, quest, Game Setup), a scene with ground and a host linked to the setup, and tests that boot the real setup and play a wave. AI guidance is now optional (checkbox / `-aiGuidance`). Recipe 10: content in the inspector
- `ActionRpg`: a playable action RPG assembled in the inspector. `Character` and `Game Setup` assets (player, enemies, waves, items, loot, quests) with validation in plain words; `ActionRpgHost` runs it with any prefab (player controls, enemy AI, strike lunge and health bar are added when missing; no prefab shows a placeholder capsule), camera follow and HUD. Engine-free rules in `ActorWorld` / `ActionRpgModule`. Requires `com.unity.inputsystem`
- `GameKit`: New Game / New Feature generators (menu and batch mode) that create the layered structure, scene, composition and tests; game architecture rules GA01-GA10 as a test base class (`GameArchitectureTests`); AI guidance installer (`CLAUDE.md` block, architecture guide, recipes, CI workflow)
- `Presentation`: `EntityView` and `EntityViewRegistry` link entities to the GameObjects that show them (pooled views can be rebound)

## [0.1.0] - 2026-10-26

First packaged release. The framework moved from `Assets/_Framework` into the embedded package
`com.aizekhan.rpg-framework`; asset GUIDs are unchanged.

- Composition: `GameHost`, `GameHostBehaviour`, ready-made gameplay modules (`Bootstrap.Gameplay`)
- Event bus integration across Combat, Progression, Quests, Inventory, Guilds and Abilities
- Asset management with Resources and optional Addressables providers; `PrefabSpawner` with pooling
- UI navigation (screens, modals, transitions); RPG panels built on it
- Persistence with checksummed save slots and metadata sidecars
