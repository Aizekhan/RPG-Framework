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

## A game in four steps

1. **Host.** Derive from `GameHostBehaviour`, put it on one object in the first scene and add modules:

   ```csharp
   public sealed class MyGameHost : GameHostBehaviour
   {
       protected override void ConfigureModules(IGameCompositionBuilder builder) => builder
           .UseModule(new CombatModule())
           .UseModule(new ProgressionModule())
           .UseModule(new QuestsModule())
           .UseModule(new ItemsModule())
           .UseModule(new LootModule())
           .UseModule(new MyGameModule());
   }
   ```

2. **Content.** Register items, quests, loot tables and stat growth in your module's `OnInitialize`.
3. **Rules.** React to framework events by subscribing to `IEventBus`: `DeathEvent`, `QuestCompletedEvent`, `LevelUpEvent`
   and so on. Do not poll other modules or call into their internals.
4. **Presentation.** MonoBehaviours read state and subscribe to events. They never own game rules.

The `Assets/ReferenceGame` project in the repository (Goblin Valley) is a complete example, with tests that boot it
through `GameHost`.

## Modules

| Area | Assemblies |
|---|---|
| Composition | `Core` (modules, builder), `DI`, `Bootstrap` (`GameHost`, `GameHostBehaviour`), `Bootstrap.Gameplay` (ready-made gameplay modules) |
| Runtime | `Events` (bus), `Systems` (update loop), `ECS` (entities, components), `Timers`, `Logging`, `Diagnostics` |
| Gameplay | `Combat`, `Stats`, `StatusEffects`, `Abilities`, `Progression`, `Quests`, `Items`, `Inventory`, `Loot`, `Crafting`, `Dialogue`, `Factions`, `Guilds`, `Trading`, `Housing`, `Companions`, `Achievements`, `Skills`, `DynamicEvents` |
| Content & scenes | `AssetManagement` (Resources and Addressables providers), `Pooling`, `Spawning` (`PrefabSpawner`, `PrefabCatalog`), `Scenes`, `Persistence` |
| UI | `UINavigation` (screens, modals, transitions), `UI` (RPG panels and HUD) |

Inside a module, engine-independent logic lives in `Runtime`, Unity-specific code in `Unity`, and tests in `Tests`.

## Rules of the architecture

- The framework never references a game assembly or game-specific types. Architecture tests enforce this.
- A module talks to another module only through contracts (interfaces) or events, never through implementations.
- Player builds contain no `UnityEditor` code outside `#if UNITY_EDITOR`.
- Every MonoBehaviour and ScriptableObject lives in a file of its own name.
