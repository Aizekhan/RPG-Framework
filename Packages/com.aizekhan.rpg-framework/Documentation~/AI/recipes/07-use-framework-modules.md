# Recipe: use framework gameplay modules

`RPGFramework.Bootstrap.Gameplay` has ready modules. Add them in the composition before the game's own modules:

```csharp
builder
    .UseModule(new CombatModule())          // ILifeStateManager (deaths on the bus), IDamageCalculator
    .UseModule(new ProgressionModule())     // IProgressionTracker, IStatGrowthFormula, level-up events
    .UseModule(new QuestsModule())          // IQuestRegistry, IQuestManager
    .UseModule(new ItemsModule())           // IItemRegistry
    .UseModule(new LootModule())            // ILootTableRegistry, ILootGenerator
    .UseModule(new CombatQuestsModule(id => KindOf(id)))  // kills advance Kill objectives
    // rpg-framework:modules (...)
    .UseModule(new MyGameModule());
```

1. Add `RPGFramework.Bootstrap.Gameplay` and the module's runtime assembly (e.g. `RPGFramework.Combat.Runtime`)
   to the Core and Tests asmdefs.
2. Declare the module in your module's `Dependencies` when you resolve its services. The host then fails loudly
   at start if the module is missing, instead of failing later.
3. Register content (items, loot tables, quests) in your module's `OnInitialize` through the registries.
4. Goblin Valley (`GoblinValleyComposition`, `GoblinValleyModule`, `GoblinValleyContent`) uses all of them.
