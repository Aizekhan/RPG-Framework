# RPG Framework

Reusable building blocks for RPGs in Unity 6. The framework owns the **rules and infrastructure**: composition, events,
combat, stats, progression, quests, items, loot, saving, assets and UI navigation. The game owns **content, presentation
and the rules that make it unique**.

## Install

The package needs UniTask. Add both lines to the game's `Packages/manifest.json`:

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
"com.aizekhan.rpg-framework": "https://github.com/Aizekhan/RPG-Framework.git?path=/Packages/com.aizekhan.rpg-framework#v0.1.0"
```

Pin a tag (`#v0.1.0`) so updates are deliberate. To develop the framework and a game side by side, point the game at a
local copy instead: `"com.aizekhan.rpg-framework": "file:../../RPG-Framework/Packages/com.aizekhan.rpg-framework"`.

Addressables are optional: when `com.unity.addressables` is installed, `AddressablesAssetProvider` compiles automatically.
Games that reference prefabs directly (prefab catalogs, view configs) never need Addressables.

## Start a game (Game Kit)

**RPG Framework > Game Kit > New Game or Feature...** creates a game that follows the architecture from the first
line, with tests that keep it that way:

```
Assets/MyGame/
  Scripts/Core/   MyGame.Core: rules, modules, events (no UnityEngine)
  Scripts/        MyGame: host (GameHostBehaviour), views, input
  Scenes/         MyGame.unity with the host
  Tests/          boot test, scene test, MyGameArchitectureTests (rules GA01-GA10)
```

**New Feature** adds a module, its rules, events and tests, and joins the module to the game's composition.
It also installs `CLAUDE.md`, `Docs/RPGFramework/architecture.md` and recipes for AI assistants, plus a CI workflow
when the project has none. **Check Architecture** runs the rules on demand. Everything also runs in batch mode, so an
AI assistant can use it:

```
Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewGameFromCommandLine -game MyGame
Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewFeatureFromCommandLine -game MyGame -feature Shop
```

The rules a game follows (each one is a test in `MyGameArchitectureTests`, failures say how to fix them):
rules in a `*.Core` assembly without UnityEngine, dependencies pointing from views to rules, no scene searches,
no singletons, immutable struct events, no UnityEditor in player code, one Unity object per file, every assembly tested.

The flow inside a game:

1. **Composition.** `MyGameComposition.Configure` lists the modules, framework ones included
   (`CombatModule`, `ProgressionModule`, `QuestsModule`, `ItemsModule`, `LootModule`); the host and the tests share it.
2. **Content.** Register items, quests, loot tables and stat growth in your module's `OnInitialize`.
3. **Rules.** React to framework events by subscribing to `IEventBus` (`DeathEvent`, `QuestCompletedEvent`,
   `LevelUpEvent`); publish your own. Do not poll other modules or call into their internals.
4. **Presentation.** A presenter spawns prefabs for entities through `IPrefabSpawner` and links them in
   `IEntityViewRegistry`; MonoBehaviours read state and call the rules. They never own game rules.

The `Assets/ReferenceGame` project in the repository (Goblin Valley) is a complete, playable example.

## Modules

| Area | Assemblies |
|---|---|
| Composition | `Core` (modules, builder), `DI`, `Bootstrap` (`GameHost`, `GameHostBehaviour`), `Bootstrap.Gameplay` (ready-made gameplay modules) |
| Runtime | `Events` (bus), `Systems` (update loop), `ECS` (entities, components), `Timers`, `Logging`, `Diagnostics` |
| Gameplay | `Combat`, `Stats`, `StatusEffects`, `Abilities`, `Progression`, `Quests`, `Items`, `Inventory`, `Loot`, `Crafting`, `Dialogue`, `Factions`, `Guilds`, `Trading`, `Housing`, `Companions`, `Achievements`, `Skills`, `DynamicEvents` |
| Content & scenes | `AssetManagement` (Resources and Addressables providers), `Pooling`, `Spawning` (`PrefabSpawner`, `PrefabCatalog`), `Presentation` (`EntityViewRegistry`: entity ↔ GameObject), `Scenes`, `Persistence` |
| UI | `UINavigation` (screens, modals, transitions), `UI` (RPG panels and HUD) |
| Tooling | `GameKit` (game and feature generators, architecture rules for games, AI guidance), `Editor` (windows, inspectors) |

Inside a module, engine-independent logic lives in `Runtime`, Unity-specific code in `Unity`, and tests in `Tests`.

## Rules of the architecture

- The framework never references a game assembly or game-specific types. Architecture tests enforce this.
- A module talks to another module only through contracts (interfaces) or events, never through implementations.
- Player builds contain no `UnityEditor` code outside `#if UNITY_EDITOR`.
- Every MonoBehaviour and ScriptableObject lives in a file of its own name.
