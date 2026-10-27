# Recipe: use framework gameplay modules

A game made with New Game already runs them: `ActionRpgHost` calls `ActionRpgComposition.Configure`, which adds the
modules below plus `ActionRpgModule`. Add the others you need (dialogue, crafting, factions...) in
`<Game>Composition.Configure`. Without the action RPG template, add them yourself before the game's own modules:

```csharp
builder
    .UseModule(new CombatModule())          // ILifeStateManager (deaths on the bus), IDamageCalculator
    .UseModule(new ProgressionModule())     // IProgressionTracker, IStatGrowthFormula, level-up events
    .UseModule(new QuestsModule())          // IQuestRegistry, IQuestManager
    .UseModule(new ItemsModule())           // IItemRegistry
    .UseModule(new LootModule())            // ILootTableRegistry, ILootGenerator
    .UseModule(new AbilitiesModule(new AbilitiesOptions { RelationshipCheck = AreEnemies }))  // IAbilityRegistry, ICooldownTracker, IAbilityExecutor
    .UseModule(new StatusEffectsModule())   // IStatusEffectManager (stat modifiers go to an IStatusEffectTarget)
    .UseModule(new CombatQuestsModule(id => KindOf(id)))  // kills advance Kill objectives
    // rpg-framework:modules (...)
    .UseModule(new MyGameModule());
```

1. Add `RPGFramework.Bootstrap.Gameplay` and the module's runtime assembly (e.g. `RPGFramework.Combat.Runtime`)
   to the Core and Tests asmdefs.
2. Declare the module in your module's `Dependencies` when you resolve its services. The host then fails loudly
   at start if the module is missing, instead of failing later.
3. Register content (items, loot tables, quests, abilities, status effects) in your module's `OnInitialize` through
   the registries. Stat growth that differs per hero or class is a profile: `growth.RegisterStatGrowth("mage", data)`
   and `growth.AssignProfile(heroId, "mage")`; stats a profile lacks use the shared growth. Level-ups then publish
   each hero's own `StatIncreasedEvent`s.
4. An entity that is gone for good: `IAbilityExecutor.UnregisterEntity(id)` drops its casts, toggles and cooldowns.
5. `ActionRpgComposition` and Goblin Valley (`GoblinValleyComposition`, `GoblinValleyModule`) use all of them.
6. For plumbing (spawning, pooling, assets, scenes, timers, logging, state machines, paths, UI screens) use the core
   services in recipe 14, never a copy of them.
