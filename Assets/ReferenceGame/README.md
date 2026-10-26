# Reference Game: Goblin Valley

A small game built only on framework assemblies. It shows how a game is composed and proves, in automated tests,
that the framework modules work together.

## Composition

`GoblinValleyComposition.Configure` adds the modules to `GameHost`. The scene host and the tests share it:

| Module | Gives the game |
|---|---|
| `FrameworkCoreModule` (added by `GameHost`) | event bus, entities, systems, timers, world |
| `CombatModule` | `ILifeStateManager` (deaths/resurrections on the bus), `IDamageCalculator` |
| `ProgressionModule` | `IProgressionTracker`, `IStatGrowthFormula`; level-ups publish `StatIncreasedEvent` |
| `QuestsModule` | `IQuestRegistry`, `IQuestManager` |
| `ItemsModule`, `LootModule` | item registry, loot tables and generator |
| `CombatQuestsModule` | kills advance Kill objectives |
| `GoblinValleyModule` | the game's content and rules (`GoblinValleyGame`) |

## Rules are bus subscriptions

| Event | Rule |
|---|---|
| `DeathEvent` (goblin) | kill XP and a loot roll for the killer; the goblin is despawned |
| `ObjectiveProgressedEvent` (completed) | the quest is turned in |
| `QuestCompletedEvent` | XP and item rewards |
| `StatIncreasedEvent` | the new stat value is applied to the hero's `StatContainer` |
| `LevelUpEvent` | the hero is healed |
| `ItemAddedEvent<IItem>` | the HUD log |

## Play

Open `Scenes/GoblinValley.unity` and press Play. The scene object `GoblinValleyHost` starts the game with three goblins.

- **Space**: attack the nearest goblin
- **N**: spawn a goblin
- **R**: revive the hero

## Tests

`Tests/GoblinValleyTests` boots the game through `GameHost` and checks:

- every module is registered;
- clearing the valley runs the whole chain: death → quest → turn-in → XP → level-up → stat growth, plus loot into the backpack;
- the world ticks without errors;
- a failing event handler is logged and the game keeps running;
- a missing module fails the start loudly;
- shutdown is clean.

`GoblinValleySceneTests` checks that the scene has its host and no missing scripts.
