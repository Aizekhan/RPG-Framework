# Recipe: action combat (attack phases, hitboxes, dodge, block, charge)

`ActionCombat` is real-time melee and ranged combat as a classic action RPG fights: an attack winds up, has a short
window in which it can hit, and recovers; **contact decides the hit** (a weapon's collider touching a body), not the
distance to the nearest enemy. Dodges are invulnerable for their own time, blocks are held and face the blow, heavy
swings and bows are held to charge. The rules (`RPGFramework.ActionCombat`, engine-free, `CombatRules`) are fully
tested; the Unity layer puts them on characters. Do not write your own attack timers, i-frame flags, "already hit"
sets or block-angle maths: they are here.

Without it, the kit's `World.Attack(attacker, target)` still works (instant hit on the nearest enemy in reach, with
the `Combat` damage calculator); use `ActionCombat` when the timing of a blow matters.

## What it does

- **Attack phases** (`AttackDefinition` / `AttackAsset`): `WindUp` → `Active` → `Recovery` seconds, a damage
  multiplier, `Stagger` (poise damage), `Interruptible` (off = hyper armour), `BlockCost`, `Tags`/`SourceId` for the
  damage rules. A combatant's state (`CombatState`): Idle, Charging, WindUp, Active, Recovery, Dodging, Blocking,
  HitReact.
- **Contact decides the hit:** `rules.ReportContact(attacker, target, swingId, point)` lands **only in the
  attacker's active window, once per target per swing**. Result (`ContactResult`): Hit, Blocked, Dodged, or Ignored
  with a reason (`ContactIgnored`: outside the window, already hit, not hostile, unknown swing, self). A hit deals
  damage through `ICombatDamage`: in a kit game that is `IActorWorld.Attack` with the attack's multiplier
  (`ActorWorldDamage`), so crits, evasion, resistances and life steal still apply.
- **Hit reaction:** a staggering hit puts the target in HitReact for its `HitReactSeconds` and interrupts an
  interruptible attack, a charge or a dodge. `Poise` absorbs stagger until it runs out (it refills after
  `PoiseRecoverySeconds` without a hit).
- **Dodge with i-frames:** `Dodge(entity, direction)` is invulnerable for exactly the dodge (`DodgeSeconds`) or a
  window inside it (`InvulnerableFrom`/`InvulnerableUntil`; on `Combatant`, a dodge clip's length and its
  `InvulnerableStart`/`InvulnerableEnd` animation events). There is no separate guard timer. A dodge cancels a charge
  or a recovery, not a blow in progress or a stagger.
- **Block is held:** `SetBlocking(entity, true/false)`. A blow is blocked only while Blocking and coming from the
  `BlockAngle` cone in front; each blocked blow costs `BlockCost` from an `IBlockStock` (stamina; none = free). When
  the stock cannot pay, the guard breaks: the blow lands and staggers. `BlockDamageMultiplier` lets a share through.
  Held during an attack, the block rises when it ends.
- **Hold to charge:** `BeginCharge(entity, attack)` (an attack with `MaxCharge` > 0), `Release(entity)`. The hold is
  clamped to [`MinCharge`, `MaxCharge`]; damage scales from 1 to `MaxChargeMultiplier` along `ChargeCurve`. A release
  under `MinCharge` is the quick attack (`QuickAttack`, or the same attack uncharged). A bow is the same thing: draw =
  charge, release = shot.
- **Projectiles:** an attack with `Projectile` opens its window when the active phase starts and keeps it for the
  flight: `CombatProjectile` reports what it meets with that swing and ends it (`EndSwing`), or
  `ProjectileLifetime` runs out. The archer recovers while the arrow flies.
- **Events** (readonly structs on the arena's bus, every one with `Time`): `AttackStartedEvent` (`Charging`),
  `AttackReleasedEvent`, `AttackActiveStartedEvent` / `AttackActiveEndedEvent`, `AttackEndedEvent` (`Interrupted`),
  `ContactResolvedEvent`, `HitReactStartedEvent` / `HitReactEndedEvent`, `DodgeStartedEvent` (with the
  invulnerability window) / `DodgeEndedEvent`, `BlockRaisedEvent` / `BlockLoweredEvent`.
- **Timeline** (`CombatTimeline`, `arena.Timeline`): every phase, contact (result, damage), stagger, dodge, block and
  refused action ("dodge while WindUp") with its exact time, also inside a long frame. `For(entity)`, `Since(time)`,
  `Mark(time, entity, note)` for the game's own marks (an input pressed). Draw a test panel from it: the monster's
  `ActiveStarted` vs the `Contact` vs the damage; the player's `DodgeStarted` vs the blow.

## Put it in a scene

1. One **`CombatArena`** in the scene (it ticks the rules; it stands still while the kit's world is paused).
2. On each character prefab: **`Combatant`** (its attacks as `AttackAsset`s, hit reaction, dodge, block). On a kit
   character the presenter gives it the entity id; elsewhere call `UseEntityId(id)`.
3. **`Hurtbox`** on the body colliders (the Combatant in the parents gives the entity). A plain entity without a
   Combatant (a crate) takes `Entity Id` on the Hurtbox; it just takes hits.
4. **`Hitbox`** on the weapon's collider (made a trigger; a kinematic Rigidbody is added when there is none above).
5. Attacks: **Create > RPG Framework > Action Combat > Attack**. Seconds, or a clip: its length split at
   `Active Start`/`Active End` (shares of the clip) or at `ActiveStart`/`ActiveEnd` animation events.
6. **`CombatAnimatorBridge`**: sets the animator's `Attack` (or the attack's own trigger), `ChargeHold`,
   `ChargeRelease`, `Dodge`, `HitReact`, `Block`; missing parameters are skipped.
7. Ranged: **`ProjectileLauncher`** (muzzle, a `CombatProjectile` prefab, speed) fires on the projectile attack's
   active phase along `Aim` (set it from the camera) or the muzzle's forward.
8. Enemies: **`CombatEnemyBrain`** instead of `EnemyBrain` (with a Combatant): it chases as before, starts its
   attack in reach and stands while it swings or is staggered. Your own AI calls the same `Combatant` API.

## The player's input

```csharp
// In your player controller (a Combatant on the same object)
if (attackPressed) combatant.Attack("slash");
if (heavyPressed) combatant.BeginCharge("heavy");
if (heavyReleased) combatant.Release();
if (dodgePressed && combatant.Dodge(moveDirection)) StartDodgeMotion(moveDirection); // the body's motion is yours
combatant.SetBlocking(blockHeld);
// Diagnostics: arena.Timeline.Mark(arena.Rules.Time, combatant.Id, "dodge pressed");
```

Movement stays the game's (the kit's `PlayerController` or your own); read `combatant.State` to stop walking while
attacking or staggered.

## Rules without Unity (tests, a server)

```csharp
var rules = new CombatRules(damage, bus, new CombatTimeline());
rules.Add(heroId, new CombatantSettings { DodgeSeconds = 0.4f });
rules.Attack(heroId, slash);
rules.Tick(0.25f);
rules.ReportContact(heroId, monsterId, rules.SwingOf(heroId));
```

## Limits
- Co-op: the rules run on the machine that owns the world (single player, the co-op host). On a co-op client a
  landed blow goes to the host as `World.Attack`, as today; the timing is the client's.
- The rules keep time; animations follow it. Make the clips match (an `AttackAsset` with a clip takes its phases
  from the clip).
