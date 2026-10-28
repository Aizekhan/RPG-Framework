# Recipe: which module a game need goes to

**Before writing a rule, find its need here.** Architecture rule GA14 fails on rules code (`<Game>.Core`) that
hand-rolls these: a weighted pick, a random amount of money, money taken from a balance, a cooldown, an effect timer,
a countdown. Only a recorded framework gap (`// framework-gap: <id>` with its section in `FRAMEWORK-GAPS.md`) lets
one through. Core services (spawning, assets, scenes, timers, logs) are in recipe 14.

| The game needs | Use | Not |
|---|---|---|
| A chest, a drop, what a monster leaves | a **loot table** (Game Setup > New Loot Table, or `LootTableBuilder`) rolled by `ILootGenerator.GenerateLootById` | `WeightedId.Pick`, `roll -= entry.Weight` |
| Gold from a monster, 5 to 20 | a loot table entry with the id `gold` and quantity 5..20; the action RPG kit pays it into the wallet | `_random.Next(GoldMin, GoldMax)` |
| Money, a balance, a price | `ICurrencyWallet` (`Balance`, `Add`, `TrySpend`, `CurrencyChangedEvent`); in the kit `World.CurrencyOf`, `GiveCurrency`, `SpendCurrency` | `profile.Gold -= price` |
| A shop, a merchant, selling loot | a **trader** with offers (`Trader.Create`, `TradeOffer.Create`, `SetGoldAmount(-price)`, `AddOfferedItem`) on `ITraderRegistry`; buying is `ITradingManager.Trade` (kit: `World.TakeOffer(offerId)`) | a dictionary of prices and manual item moves |
| A quest that pays | a quest reward `RewardType.Currency` (id `gold`, amount) | granting money in a quest handler |
| A perk, a skill, a dash, any action with a cooldown | an **ability** (Ability asset or `AbilityMetadata`, in `content.Abilities`); what it does: `ActorAbilities.SetEffect(id, effect)`; use: `World.UseAbility(id)` | a `switch` over perk ids with `_cooldowns[id] -= dt` |
| Poison, burning, a shield, a slow, a stun, a speed boost | a **status effect** (Status Effect asset or `StatusEffectData`, in `content.StatusEffects`), `World.Effects.Apply(target, id, source)` | `_poisonLeft -= dt` and a tick counter |
| "After 3 seconds", "every 2 seconds" | `ITimerService.Schedule` / `Repeat` / `DelayAsync` | `_spawnIn -= dt` |
| A wave of enemies | `WaveSettings` in the Game Setup (`WaveSystem`) | a spawn timer in a rule |
| Experience, levels, stat growth | `IProgressionTracker`, `IStatGrowthFormula` (kit: `World.GrantExperience`) | `xp += n; if (xp > 100) level++` |
| A backpack | `IInventoryContainer<IItem>` (kit: `World.InventoryOf`, `GiveItem`) | `List<string> items` |
| Quests and objectives | `IQuestManager` + Quest assets (recipe 10) | flags in a dictionary |
| A chest, a door, a lever, a shrine: "press E near it" | a **place**: `InteractionPointView` in the scene (kit) or `IInteractionService.Add(new InteractionPoint(id, kind, radius, prompt, once))` (`InteractionModule`); react to `InteractedEvent` in a module; a chest pays with `World.GiveLoot(player, table)`; a locked door is `SetRule(kind, ...)` | a trigger collider and `Vector3.Distance` checks in an `Update` |
| A screen: stats, equipment, a shop, a pause menu | `StatListPanel`, `EquipmentSlotsPanel`, `ItemSlotUI.Create` (they build themselves) registered on `CodeViewFactory`; kit: `ActionRpgHost.Screens` / `ConfigureScreens`, opened with `Navigation.NavigateToAsync(id)` | a hand-built canvas per screen and a bool for which one is open |
| Talking to a character | `Dialogue` (`DialogueManager`, conditions, branches, events) | a script of `if`s |
| Crafting | `Crafting` (recipes, requirements, timed crafts) | a recipe table checked by hand |
| Reputation with a side | `Factions` | a number per faction |
| Saving all of it | `SaveParticipant<T>` (recipe 12); the kit saves progress, backpacks and money | own JSON files |

## The wallet outside the action RPG kit
Add `TradingModule` to the composition (it registers `ICurrencyWallet`, `ITraderRegistry`, `ITradingManager`). Give
it an `ITradeInventory` (`TradingOptions.Inventory`, or register one in the container) so trades move items in your
inventory; without one, trades only report the items. Save `wallet.BalancesOf(owner)` in a `SaveParticipant<T>` and
put them back with `SetBalance`.

## When the module almost fits
Use it and add what is missing through its hooks: an ability's effect callback, `ResolveTarget` and
`AbilityDistance` (recipe 17), the trading inventory, a damage calculator. If it truly cannot do it, record the gap
(GA12) and keep a small stand-in marked `// framework-gap: <id>`; after a framework update, check whether the gap
closed (CLAUDE.md, "After the framework is updated").
