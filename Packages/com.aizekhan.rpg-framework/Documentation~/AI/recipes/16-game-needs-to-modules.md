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
| A perk, a skill, a dash, any action with a cooldown | an **ability** (Ability asset or `AbilityMetadata`, in `content.Abilities`); what it does: `ActorAbilities.SetEffect(id, effect)` (return false when it found nothing: cost and cooldown come back); when it may be used: `CanUse`; its cooldown this time: `CooldownFor`; use: `World.UseAbility(id)` | a `switch` over perk ids with `_cooldowns[id] -= dt` |
| A strike that lands at its contact, an arrow's flight, a meteor that falls later, a flurry of stabs | the ability's **hits**: `CastInfo.ImpactDelay` (Ability asset: Impact Delay), `ImpactCount` + `ImpactInterval` for several, `ImpactsSurviveInterrupt` for an arrow already loosed, `RecheckTargetOnImpact` for a target that may roll away. Paid and cooled down at the use, the effect runs at each hit; attack speed: `ActorAbilities.ImpactSpeedFor = (caster, ability) => speed`. A stun or death stops a swing before contact; `Abilities.Interrupt(id)` for a heavy blow. No delay: it hits at once, as before | dealing the damage at the button press while the clip is still winding up, or a coroutine `WaitForSeconds` before the damage |
| Poison, burning, a shield, a slow, a stun, a speed boost | a **status effect** (Status Effect asset or `StatusEffectData`, in `content.StatusEffects`), `World.Effects.Apply(target, id, source)`; stronger or shorter this time: `Apply(target, id, source, power, duration)`; what killed: `ActorKilledEvent.Context` (`FromEffect`, `SourceId`) | `_poisonLeft -= dt` and a tick counter |
| Pausing the game: a vote, a menu, a cutscene | `IGamePause.Pause(reason)` / `Resume(reason)` (systems tick with no time passing; the kit's controls and enemies stand still, co-op friends too) | a `bool _paused` checked in every rule |
| "After 3 seconds", "every 2 seconds" | `ITimerService.Schedule` / `Repeat` / `DelayAsync` | `_spawnIn -= dt` |
| A wave of enemies | `WaveSettings` in the Game Setup (`WaveSystem`) | a spawn timer in a rule |
| Experience, levels, stat growth | `IProgressionTracker`, `IStatGrowthFormula` (kit: `World.GrantExperience`) | `xp += n; if (xp > 100) level++` |
| A backpack | `IInventoryContainer<IItem>` (kit: `World.InventoryOf`, `GiveItem`) | `List<string> items` |
| Quests and objectives | `IQuestManager` + Quest assets (recipe 10) | flags in a dictionary |
| A quest that branches ("golem beaten" → return, "died three times" → retreat), a daily quest, "needs level 5 and the guild's trust" | quest **stages** with conditions, `Signal`, `RepeatCooldownSeconds`, `Requirements` (recipe 20; kit: `World.SignalQuests`, `WhyNotQuest`) | a `questStep` int and a `switch` |
| Quests and dialogues written in another tool or a spreadsheet | a content JSON file (`rpgframework.content/1`, `ContentImporter`; kit: Game Setup **Content Files**; recipe 20) | a JSON parser of the game's own |
| A chest, a door, a lever, a shrine: "press E near it" | a **place**: `InteractionPointView` in the scene (kit) or `IInteractionService.Add(new InteractionPoint(id, kind, radius, prompt, once))` (`InteractionModule`); react to `InteractedEvent` in a module; a chest pays with `World.GiveLoot(player, table)`; a locked door is `SetRule(kind, ...)` | a trigger collider and `Vector3.Distance` checks in an `Update` |
| Random bonuses on dropped gear ("Sturdy Sword of the Bear"), rarity, magic find | **affixes**: `AffixPool` + `RarityTable` (Items); in the kit set `content.Affixes` / `Rarities` / `LuckOf` and dropped gear rolls them (ids from `RolledItems`) | a list of bonus names and `_random.Next` per stat |
| A world map: towns joined by roads, where each player stands, places discovered, fast travel back | **`WorldMap`**: a `WorldGraph` (`AddPlace`, `AddRoute(id, from, to)`; a route is a road, a track or a level) and `IWorldTravel` (`WorldMapModule(map)`): `LocationOf`, `RoutesHere`, `Arrive(player, route)` at a road's end, `Travel(player, place)` to a discovered place, `TravelRule` for the game's say, `TravelTogether` for a party that moves as one, `PlaceEnteredEvent`; saved by `WorldTravelSave`; in co-op the host sends `Capture(player)` in a game message and guests `Restore` it | a current-town string and a visited list in the profile |
| A settings page: screen, quality, volumes, language | `RPGFramework.Settings` (`ISettingsService`, `DisplaySettings`, `SettingsBridges`, `SettingsRows` on an `ActionListPanel`; kit: `ActionRpgHost.Settings`) | PlayerPrefs calls and a hand-made options screen |
| An aura that drains mana while on, a channel that costs per second | `ResourceCost.PerSecond` on a Toggle or Channeled ability (`AbilityToggledEvent.Reason` when it runs dry) | `mana -= cost * dt` in an `Update` |
| Attacks with a wind-up and a hit window, weapon hitboxes, dodge with i-frames, a held block, charged swings and bows | **`ActionCombat`** (recipe 18): `CombatArena`, `Combatant`, `Hitbox`/`Hurtbox`, `AttackAsset`; `CombatRules` without Unity | an attack timer, an `_invulnerable` flag and a `HashSet` of hit enemies in a controller |
| Monsters that notice, circle, take turns attacking, pick moves and combos, block and dodge | **`CombatEnemyAI`** + an Enemy Behaviour asset (`EnemyDirector`, attack tokens; recipe 18) | a state machine per monster with its own attack cooldown and "who attacks now" flags |
| Many monsters chasing or fleeing the player on a grid | `DistanceMap` (Pathfinding): one `Build(goals)`, then `NextStep` / `StepAway` per monster | an A* per monster per frame |
| A screen: stats, equipment, a shop, a pause menu | `StatListPanel`, `EquipmentSlotsPanel`, `ActionListPanel` (rows with their own buttons: trainer, shop, smith), `ItemSlotUI.Create` (they build themselves; sizes from code) registered on `CodeViewFactory`; kit: `ActionRpgHost.Screens` / `ConfigureScreens`, opened with `Navigation.NavigateToAsync(id)` | a hand-built canvas per screen and a bool for which one is open |
| Talking to a character | `Dialogue` (`DialogueManager`, conditions, branches, events; localized lines, barks, kit effects that move quest stages or raise the game's event: recipe 20) | a script of `if`s |
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
