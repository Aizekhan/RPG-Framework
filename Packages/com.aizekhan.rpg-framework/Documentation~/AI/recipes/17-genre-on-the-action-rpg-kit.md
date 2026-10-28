# Recipe: another genre on the action RPG kit (a runner, lanes, a grid)

The kit (`ActionRpgHost`, `ActorWorld`) assumes characters that walk in free 3D. A runner, a lane brawler or a grid
game still gets most of the kit: characters, health, attacks, deaths, waves, loot, currency, quests, progress, saves,
abilities, status effects and co-op. What changes is **where things are**, and that belongs to the game. Do not
let it pull the framework's work into one big class: MythRunner's rules ended up in a 1774-line class with its own
cooldowns, timers, loot and shop inside it, because the positions were the game's and everything else followed.

## 1. Keep the game's space in one place
Write one rules class for the space only (`Track`: distance along the track and lane of each character, the speed,
the obstacles). It knows nothing about health, cooldowns or loot. Everything else asks it:

| The kit asks | You answer, from your space |
|---|---|
| ability range (`Range` on the ability) | override `ActionRpgHost.AbilityDistance(casterId, targetId)`: `Math.Abs(track.At(a) - track.At(b))` (default: scene distance) |
| who an ability without a target hits | `World.Abilities.ResolveTarget = (caster, ability) => track.NearestAhead(caster)` (return -1 for none) |
| where a character respawns | `IRespawnPointProvider` (recipe 11) |
| whether an attack on a friend lands (dodged by a lane change) | owner-decided defence: `host.DefenseAuthority = DefenseAuthority.Owner`, the friend answers `client.Defend(...)` (recipe 11) |

## 2. The game's own actions are ability effects, not a switch
A perk, a dash, a rewind, a ram, a lane change: each is an **ability** (Ability asset or `AbilityMetadata`, added to
`content.Abilities` in `ConfigureSettings`). The framework keeps its cooldown, charges, cast time, cost, the stun
that blocks it and the co-op sync; the game writes only what it does:

```csharp
// in a game module (ConfigureGameModules), with ActorWorld and the Track from the container
world.Abilities.SetEffect("lane-left", (caster, target) => track.ChangeLane(caster, -1));
world.Abilities.SetEffect("rewind", (caster, target) => track.Rewind(caster, seconds: 3f));
world.Abilities.SetEffect("ram", (caster, target) => world.DealDamage(caster, target, 20f, AttackContext.Effect(caster)));
```

Input calls `World.UseAbility("ram")` (or `Abilities.Use(playerId, "ram")`); a joined friend's use reaches the host
as an intent and runs there, with the host's `ResolveTarget`. An area hit is the effect looping over `track.Within(caster, radius)`.

## 3. Timed states are status effects
Poison, a shield, a speed boost, "frozen", invulnerability after a hit: a **status effect** (Status Effect asset or
`StatusEffectData`, in `content.StatusEffects`), applied with `World.Effects.Apply(target, "shield", source)`. The
framework runs its duration, ticks, stacks, stat changes and controls (`Stun`, `Silence`), and ends it with the
character. A rule that needs to know asks `World.Effects.Has(id, "shield")` or subscribes to the effect events.

## 4. One feature, one module
Split the rules with **New Feature** (recipe 02): `Track`, `Obstacles`, `Perks`, `Chests`, `Shop`, each with its
module, events and tests. Features talk through the bus (`ObstacleHitEvent`, `ChestOpenedEvent`), not by calling
each other. A class that grows past a few hundred lines is a sign that a module's job got pulled into it; check
recipe 16 before writing a countdown, a weighted pick or a price.

## 5. What stays a framework gap
If the kit cannot express something even with the hooks above (it really needs positions it does not have), record
it in `FRAMEWORK-GAPS.md` and keep the smallest stand-in (GA12). Do not copy the kit's rules into the game.
