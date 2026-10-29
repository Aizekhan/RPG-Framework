# Recipe: a turn-based party RPG

`TurnBased` is the rules of a JRPG-style game: a party of heroes fights a run of encounters in turns. It is
engine-free and fully tested. `TurnBasedHost` runs it in a scene with a battle screen built from code
(`BattleScreen`); a game with its own look builds its views on `ITurnBasedWorld`, `Battle` and `BattleMenu`. Do not
write your own turn order, battle menu,
damage maths, cooldowns, status effects, rewards or levels: they are here, built on the framework's Abilities,
StatusEffects, Progression, Items, Inventory and Loot modules.

## What it does

- **Turns:** order by Speed each round (ties: party first, then slot order). A fallen combatant leaves the order.
- **Actions** (`BattleAction`): `Attack(target)`, `Ability(id, target)`, `Defend()`, `Item(id, target)`, `Flee()`.
  `battle.Perform(action)` acts for the current combatant; a refused action changes nothing and says why
  (`ActionResult.Refusal`, or ask first with `battle.WhyNot(action)` / `CanPerform`).
- **Abilities:** an `IAbility` (Abilities module) gives the mana cost and the cooldown in rounds; an
  `AbilityEffectData` with the same id says what it does (damage, heal, support; single or all enemies or allies;
  status effects with a chance; revive). Instant abilities only.
- **Status effects** (StatusEffects module): poison and regen tick at the start of the affected combatant's turn,
  `Combatant.Stun` (a Control modifier) skips the turn, stat modifiers change Attack/Defense/Magic/Speed. Durations
  count the affected combatant's own turns.
- **Enemies** act by `IBattleAi` (default `SimpleBattleAi`: heals a hurt ally, else its strongest affordable attack
  on the weakest hero). `EnemiesActAutomatically` (default true) plays their turns right after the party's.
- **Damage** by `IBattleFormula` (default seeded; `Seed` in the settings makes runs repeatable).
- **The run:** encounters in order; victory gives XP (levels raise stats by each hero's growth), gold and loot into
  the party inventory; `RestAfterVictory` heals between fights; defeat ends the run until `Restart()`.
- **Events** on the bus: `BattleStartedEvent`, `RoundStartedEvent`, `TurnStartedEvent`, `TurnSkippedEvent`,
  `ActionPerformedEvent`, `CombatantDefeatedEvent`, `BattleEndedEvent` (with rewards), `MemberLevelUpEvent`,
  `RunCompletedEvent`. Status effect events come from the StatusEffects module.
- **Saves:** `TurnBasedSaves` keeps levels, XP, health, mana, inventory, gold and where the run stands (between
  battles; a battle in progress is not saved). See recipe 12.

## Start it

```csharp
public sealed class MyGameHost : TurnBasedHost
{
    [SerializeField] private MyGameConfig _config; // your ScriptableObject with the party, enemies, encounters

    protected override void ConfigureGame(TurnBasedSettings settings, TurnBasedContent content)
    {
        _config.FillSettings(settings);
        _config.FillContent(content);
    }
}
```

Put it on an object in the scene and press Play: the battle screen (enemies, party, log, the turn's menu, a command
then its target, "Fight" for the next encounter, "New run" after a defeat) is built from code. In the inspector:
`Enemy Turn Seconds` (enemies act one by one so the player sees them; 0: at once), `Saves` (save after every battle
in `Application.persistentDataPath/<Save Folder>` and continue the run on start), `Battle Screen` (off: build your
own on `host.World`, `host.StartBattle()`, `host.Act(action)`, `host.NewRun()`).

Without the host (another host, a test), compose it yourself:
`Game = TurnBasedComposition.Configure(builder, settings, content);`

`TurnBasedSettings`: `Party`, `Enemies` (`CombatantData`: stats, growth per level, `AbilityIds`, rewards),
`Encounters` (`EncounterData`: enemy ids, `CanFlee`, `EnemyLevel`), `StartingItems`, `StartingGold`, `FleeChance`,
`Seed`, `RestAfterVictory`, `Formula`, `EnemyAi`. `settings.Validate(content)` lists problems in plain words.

`TurnBasedContent`: `Items`, `LootTables`, `Abilities`, `StatusEffects`, `AbilityEffects`. Fill it from the designer
assets so content stays in the inspector: `ItemAsset.ToItem()`, `LootTableAsset.ToLootTable()`,
`AbilityAsset.ToAbility()`, `StatusEffectAsset.ToData()` (`Tools/*Editor`, Create > RPG Framework). Consumables use
`ItemEffects` ids (`RestoreHealth`, `RestoreMana`, `Revive`, `Cure`, `Damage`); any other effect id is applied as a
status effect.

## Drive it from the screen

`BattleMenu.For(battle, world.Inventory.GetAllItems())` gives the current hero's commands as the rules see them: each
`BattleCommand` has a `Label`, `NeedsTarget` with its `Targets`, or a `Refusal` ("Not enough mana..."); aim it with
`command.With(target)` and pass that to `battle.Perform`. By hand:

```csharp
var world = host.Game.World;               // ITurnBasedWorld for views
if (world.WhyCannotStartBattle() == null)
    world.StartBattle();

var battle = world.Battle;
var hero = battle.Current;                 // whose turn it is (a party member while the screen waits)
var target = battle.LivingOpponents(hero)[0];
var result = battle.Perform(BattleAction.Attack(target.Id));
if (!result.Done)
    ShowMessage(result.Refusal);           // e.g. "Not enough mana: Fire costs 6 MP and Mage has 2."
```

Show `battle.TurnOrder`, each `Combatant`'s `Health`/`MaxHealth`/`Mana`, `battle.CooldownLeft(id, abilityId)` and
`battle.Log`; animate from `ActionPerformedEvent` (`TargetOutcome`: damage, healing, missed, defeated, revived).
`battle.SuggestAction()` gives an auto-battle choice for the current hero. When `battle.IsOver`, read
`battle.Outcome` and show the `BattleEndedEvent` rewards, then start the next encounter.

## Tests

`TurnBased/Tests/TurnBasedTestGame.cs` is a complete small game (knight, mage, cleric against goblins, a shaman and an
ogre) composed through `GameHost`: copy its setup for your own tests. Keep `Seed` fixed and `DefaultBattleFormula`
with `MissChance = 0` and `Variance = 0` when a test needs exact numbers.
