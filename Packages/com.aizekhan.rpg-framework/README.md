# RPG Framework

Reusable building blocks for RPGs in Unity 6. The framework owns the **rules and infrastructure**: composition, events,
combat, stats, progression, quests, items, loot, saving, assets and UI navigation. The game owns **content, presentation
and the rules that make it unique**.

## Install

The package needs UniTask. Add both lines to the game's `Packages/manifest.json`:

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
"com.aizekhan.rpg-framework": "https://github.com/Aizekhan/RPG-Framework.git?path=/Packages/com.aizekhan.rpg-framework#v0.14.1"
```

Input System and Test Framework come with it; if Unity asks to enable the new input backend, answer Yes (it restarts the
editor once). Pin a tag (`#v0.14.1`) so updates are deliberate. To develop the framework and a game side by side, point the game at a
local copy instead: `"com.aizekhan.rpg-framework": "file:../../RPG-Framework/Packages/com.aizekhan.rpg-framework"`.

Addressables are optional: when `com.unity.addressables` is installed, `AddressablesAssetProvider` compiles automatically.
Games that reference prefabs directly (prefab catalogs, view configs) never need Addressables.

## Make a game in 5 minutes

1. **RPG Framework > Game Kit > New Game or Feature...**, type a name, press **New Game**. The scene opens.
2. Press **Play**. A hero (blue capsule) walks with WASD / left stick and attacks with Space / left click; goblins come
   in waves, drop loot and count for the first quest; the HUD shows health, level, quests, wave and a log.
   **I** / **Tab** opens the character sheet and backpack. Progress (level, backpack, quests) is saved on level-up,
   finished quests and quit, and comes back next Play; untick **Save Progress** on the host to start fresh every time,
   or right-click the host > **Delete Saved Progress**.
3. Make it yours in the inspector, no code:
   - `Assets/MyGame/Content/MyGameSetup.asset` (**Game Setup**): the player, the enemies, waves, items, loot tables,
     starting quests, inventory size, random seed, camera offset. A box at the top says what is wrong before you press
     Play; buttons create a new enemy, item, loot table or quest next to it.
   - `Content/Characters/*.asset` (**Character**): drag any model prefab into **Prefab** (pivot at the feet, facing +Z),
     set health, attack, defense, growth per level, speed, reach, attack rate, experience and loot table. Movement
     (player), AI (enemies), a strike lunge and a health bar are added in play mode when the prefab has none; add
     `PlayerController`, `EnemyBrain`, `StrikeAnimator` or `HealthBarView` to the prefab yourself to tune them.
     With an Animator, set the strike's trigger name to play your attack clip.
   - Items, loot tables and quests are the framework's **Item**, **Loot Table** and **Quest Asset** assets
     (Create > RPG Framework). Drag a sprite into an item's **Icon** to show it in the backpack.
   - **F3** in play mode shows the diagnostics panel (systems, events, frame times); untick **Diagnostics** on the
     host to leave it out. Release builds never include it.
4. Rules the inspector cannot express go in code: `Scripts/Core/MyGameSession.cs`, or **New Feature** for a module of
   their own. They react to events (`ActorSpawnedEvent`, `ActorDamagedEvent`, `WaveStartedEvent`, `DeathEvent`,
   `QuestCompletedEvent`, `LevelUpEvent`) and act through `ActorWorld`.

Without the Game Kit: create a **Game Setup** (Create > RPG Framework > Game Setup) and **Characters**, put an
**Action Rpg Host** on an empty GameObject, drag the setup into it, add a ground with a collider and press Play.

What a new game contains:

```
Assets/MyGame/
  Content/        Game Setup, characters, items, loot table, quest, ground material (edit these)
  Scripts/Core/   MyGame.Core: your rules and modules (no UnityEngine)
  Scripts/        MyGame: MyGameHost (an ActionRpgHost), your views and input
  Scenes/         MyGame.unity: host, camera, light, ground
  Tests/          boot test, scene test, MyGameArchitectureTests (rules GA01-GA14)
```

The tests keep the game healthy: the setup is valid, the game boots and plays a wave, the code follows the
architecture rules. Run them in **Window > General > Test Runner**.

### Game Kit details

**New Feature** adds a module, its rules, events and tests, and joins the module to the game's composition.
**Check Architecture** runs the rules on demand. Tick **Also install AI guidance** on New Game (or use the button) to
add `CLAUDE.md`, `Docs/RPGFramework/architecture.md` and recipes for AI assistants, plus a CI workflow when the project
has none. Everything also runs in batch mode, so an AI assistant can use it:

```
Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewGameFromCommandLine -game MyGame [-aiGuidance]
Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewFeatureFromCommandLine -game MyGame -feature Shop
```

The rules a game follows (each one is a test in `MyGameArchitectureTests`, failures say how to fix them):
rules in a `*.Core` assembly without UnityEngine, dependencies pointing from views to rules, no scene searches,
no singletons, immutable struct events, no UnityEditor in player code, one Unity object per file, every assembly tested.

Under the hood (for games that outgrow the action RPG template):

1. **Composition.** `GameHost` runs `GameFeatureModule`s; `ActionRpgComposition` adds the framework gameplay modules
   (`CombatModule`, `ProgressionModule`, `QuestsModule`, `ItemsModule`, `LootModule`) and `ActionRpgModule`.
2. **Content.** Registries (`IItemRegistry`, `ILootTableRegistry`, `IQuestRegistry`) are filled from the assets.
3. **Rules.** React to framework events by subscribing to `IEventBus`; publish your own. Do not poll other modules or
   call into their internals.
4. **Presentation.** `ActorPresenter` spawns prefabs through `IPrefabSpawner` and links them in `IEntityViewRegistry`;
   MonoBehaviours read state and call the rules. They never own game rules.

The `Assets/ReferenceGame` project in the repository (Goblin Valley) is a complete, playable example.

## Genres

| Genre | What you get | Start with |
|---|---|---|
| **Action RPG** (real-time, top-down or third person) | A playable game: hero, enemies, waves, loot, quests, HUD, saves, co-op for 2-8 players and PvP, all set up from inspector assets | **New Game** in the Game Kit (recipe 01) |
| **Turn-based party RPG** (JRPG style) | The rules, tested: turn order, attacks, abilities with mana and cooldowns, defend, items, flee, status effects, enemy AI, rewards, levels, a run of encounters, saves. **No battle screen yet**: the game builds its own views on `ITurnBasedWorld` | `TurnBasedComposition.Configure` (recipe 15) |
| Anything else (roguelike, narrative, survival...) | The gameplay modules below, the core services (recipe 14) and the architecture rules | A New Game, then replace what does not fit |

## Modules

| Area | Assemblies |
|---|---|
| Composition | `Core` (modules, builder), `DI`, `Bootstrap` (`GameHost`, `GameHostBehaviour`), `Bootstrap.Gameplay` (ready-made gameplay modules), `Bootstrap.UnityServices` (`UnityServicesModule`: shared pools, assets, prefab spawner, scenes) |
| Runtime | `Events` (bus), `Systems` (update loop), `ECS` (entities, components), `Timers`, `Logging`, `Diagnostics` |
| Game templates | `ActionRpg` (Game Setup and Character assets, `ActionRpgHost`, player controls, enemy AI, health bars, HUD); `TurnBased` (party battles in turns, rules only) |
| Gameplay | `Combat`, `Stats`, `StatusEffects`, `Abilities`, `Progression`, `Quests`, `Items`, `Inventory`, `Loot`, `Crafting`, `Dialogue`, `Factions`, `Guilds`, `Trading`, `Housing`, `Companions`, `Achievements`, `Skills`, `DynamicEvents`, `Dice` (seeded dice with a log, `ScriptedDice`), `Cards` (`Deck<T>`, `Hand<T>`, `DeckAsset<T>`) |
| Content & scenes | `AssetManagement` (Resources and Addressables providers), `Pooling`, `Spawning` (`PrefabSpawner`, `PrefabCatalog`), `Presentation` (`EntityViewRegistry`: entity ↔ GameObject), `Scenes`, `Persistence` |
| UI & sound | `UINavigation` (screens, modals, transitions), `UI` (RPG panels and HUD), `Audio` (`IAudioService`: cues with variants and voice limits, music crossfades, volume channels kept in PlayerPrefs as `RPGFramework.Audio.<channel>` (or the game's own `IVolumeStore` given to `AudioModule`); `AudioModule`, `AudioLibrary`) |
| Networking | `Networking` (messages, authority, `UnityNetworkTransport` over UDP, `LoopbackNetwork` for tests); action RPG co-op for 2-8 players (F2 in any `ActionRpgHost`), see `Documentation~/networking.md`; `Turns` (host-run turn-based matches for board and card games: lobby, moves checked by `ITurnRules`, a view per seat for hidden cards, notices, reaction prompts, turn timer, rejoin) |
| Tooling | `GameKit` (game and feature generators, architecture rules for games, AI guidance), `Editor` (windows, inspectors) |

Inside a module, engine-independent logic lives in `Runtime`, Unity-specific code in `Unity`, and tests in `Tests`.

## Rules of the architecture

- The framework never references a game assembly or game-specific types. Architecture tests enforce this.
- A module talks to another module only through contracts (interfaces) or events, never through implementations.
- Player builds contain no `UnityEditor` code outside `#if UNITY_EDITOR`.
- Every MonoBehaviour and ScriptableObject lives in a file of its own name.
