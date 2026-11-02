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
  `PoiseRecoverySeconds` without a hit). **By force** (the attack's `Stagger`): below `HeavyStagger` a light
  flinch, from it a heavy stagger (`HeavyHitReactSeconds`, pushed back `HeavyKnockback` m), from `KnockdownStagger`
  a knockdown with get-up (`KnockdownSeconds`, `KnockdownKnockback`). `HitReactStartedEvent` carries `Tier`,
  `Knockback` and `Direction` (away from the attacker); the Unity side moves the body.
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
- **The attack's shape** (an `AttackAsset`'s Hold, Motion and Sweep sections):
  - `HoldWindUp(entity, true/false)` holds a wind-up at its `HoldPoint` (share of the wind-up; 1 = just before the
    blow) until let go: a wide swing waits raised. `AnimationPaceOf` is 0 meanwhile, so the clip stands too;
    `WindUpHeldEvent`.
  - `Motions`: per phase an `AttackMotion` (`Approach`, `Retreat`, `StrafeLeft`/`StrafeRight`, `Leap` with a
    `Height`) covers its `Distance` over a part of the phase (`From`/`Until`). `MotionOf(entity)` is the speed now
    in the attacker's space (`InWorld(forward)` for a velocity); the enemy director moves its bodies by it, a player
    controller may too. It follows the attack speed.
  - `Sweep`: the weapon's volume over the active window as `SweepSegment`s in time (`From`/`Until` of the window),
    each an arc around the attacker (`ArcFrom`..`ArcTo` degrees from its forward, right positive; `InnerRadius`..
    `Radius`; `HeightMin`..`HeightMax` above its feet, both 0 = any). `Sweep(attacker, target, position, radius)`
    reports a contact only when the body is inside the segment active at that moment: weapon contact without
    physics. `TryGetSweepSegment` gives it to draw (a `Hitbox` draws its owner's in the scene view).
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
   Attack speed: set `combatant.AttackSpeed` (or `rules.SetAttackSpeed(id, k)`) from the game's stats. 1.5
   makes wind-up, active window and recovery last 1/1.5 as long; set mid-swing, the phase keeps its progress.
   Add a float `AttackPace` to the controller and tick **Multiplier** on the attack states with it: the bridge
   feeds it `rules.AnimationPaceOf(id)`, so the clip's contact frame stays on the rules' active window at any speed.
7. Ranged: **`ProjectileLauncher`** (muzzle, a `CombatProjectile` prefab, speed) fires on the projectile attack's
   active phase along `Aim` (set it from the camera) or the muzzle's forward.
   Without physics (no colliders: a runner's road, a server): **`CombatProjectiles`** beside the rules,
   `new CombatProjectiles(enemyWorld, bus, rules)`, ticked after the bodies move. `Launch(new ProjectileLaunch { ShooterId,
   TargetId, From, Velocity, Radius, Seconds, Mode })`: `Landing` (an arrow in an arc) strikes everyone of
   `IEnemyWorld.TargetsOf(shooter)` within its radius where it comes down, `Contact` (a bolt) the first on its path
   (`Pierce` for more). `CombatProjectiles.Lead(from, target, targetVelocity, speed, out velocity, out seconds)`
   aims where a moving target will be. `VoidIfShooterDies` drops a fallen archer's arrow harmless; `SwingId` (an
   attack with `Projectile`) sends each strike through `ReportContact`, so dodges and blocks count. Events:
   `ProjectileLaunchedEvent`, `ProjectileStruckEvent`, `ProjectileLandedEvent` (`Struck`, `Landed`,
   `MissedItsTarget`, `Voided`). Views read `InFlight`; a co-op friend launches `Mirror = true` copies under the
   host's `Id` (they fly, the host decides the strikes).
8. Enemies: **`CombatEnemyAI`** (below) for monsters that fight like in an action RPG; the simple
   **`CombatEnemyBrain`** chases as `EnemyBrain` does and starts its attack in reach. Your own AI calls the same
   `Combatant` API.

## Enemy combat AI

`EnemyDirector` (engine-free, `arena.Enemies`) decides for every enemy each frame; `CombatEnemyAI` (replaces
`EnemyBrain` on the prefab, with a `Combatant`) moves the body along its `EnemyIntent`. Tune a kind of monster in an
**Enemy Behaviour** asset (Create > RPG Framework > Action Combat > Enemy Behaviour):

- **Perception:** `SightRadius`, `ViewAngle` (cone) and a line of sight (a raycast from `Eye Height`, blocked by the
  arena's `Sight Blockers`); `HearingRadius` notices anyone close; being struck alerts at once. `MemorySeconds` of
  chasing after losing sight, `LeashRadius` from home, then it goes home. `NoticeSeconds` of a roar first.
- **Movement** (each with its speed): Idle, Patrol (the component's waypoints, or `WanderRadius` around home),
  Chase (run beyond `EngageDistance`), Approach (walk in to strike), Circle (strafe at `CircleDistance` while
  waiting), BackOff (too close while waiting, or hurt), Returning. `TurnSpeed`; no turning in an attack's hit window
  (`TrackDuringWindUp` per move for the wind-up). Enemies keep `PersonalSpace` from each other.
- **Attack tokens:** at most `Attackers Per Target` (arena, default 2) enemies attack one target at once
  (`AttackTokens`); the others circle. Free slots go to the nearest and longest waiting. A token is given back when
  the attack or combo ends or the enemy is staggered. A holder in reach with no move ready (cooldowns) gives its
  turn back ("no move ready"), circles and asks again when a move is ready; it steps back only when the target is
  closer than every move's `MinRange`.
- **Moves:** an attack with `MinRange`/`MaxRange` (a lunge is a gap closer from range), `Weight`, `Cooldown`,
  `Next` (a combo link played only when the previous blow hit or was blocked; `Opener` off = only as a link). Picked
  by distance, cooldowns and weights with the arena's `Seed`. A move starts only within `CommitRange` (`MaxRange` x
  `CommitShare`; 0.75 makes a dagger close to three quarters of its reach first, so a target that keeps moving is
  still in reach when the blow lands). `HoldUntilInRange` (a wide swing) may start anywhere in `MaxRange` and holds
  its wind-up, tracking and closing in at `HoldSpeed` (0 = `ApproachSpeed`), until the target is within the commit
  range or `MaxHold` runs out. The attack's `Motions` move the body during its phases (an approach stops at
  `ContactDistance` from the target); a leap's `EnemyIntent.Height` raises `CombatEnemyAI`'s `Leap Root`.
  `arena.Enemies.SweepAttacks = true` lands the blows by their `Sweep` segments (`SweepRadius` for the target's
  body), for a world without colliders.
- **Weapon move sets:** a **Weapon Move Set** asset (Create > RPG Framework > Action Combat > Weapon Move Set) holds
  a kind of weapon's moves (each strike clip with its motion, sweep, ranges, commit, hold and combo links). An Enemy
  Behaviour's `Weapon` takes its moves from it; its own list adds the monster's. After an attack or combo, `RecoveryPause`: the
  player's punish window. The asset's `Problems()` (logged as warnings in the inspector) catches a move without an
  attack, a combo link to itself or out of the list, and no opener with a weight. Moves added from code through
  `SerializedProperty` start at zero (Weight 0, Next 0): set Weight and Next = -1.
- **Defence:** when its target starts a swing: `BlockChance` (held `BlockSeconds`), else `DodgeChance` (sideways);
  under `BackOffHealthShare` of health a `BackOffChance` to step away.
- **Hit reactions** by force through the `Combatant`'s hit tiers (light, heavy with push back, knockdown and
  get-up); big moves with `Interruptible` off keep going (hyper armour).
- **Death:** `EnemyDiedEvent` with `Variant` (one of `DeathVariants` clips). The kit keeps the body for the
  character's **`Corpse Seconds`** (Character asset) before the view goes; `ActorPresenter.CorpseRemoving` is the
  hook for a fade. Any `ActorView` hears `OnKilled()` while still bound.
- **Animation:** `EnemyAnimatorBridge` sets `Speed`, `MoveX`/`MoveY` (local m/s, for a strafe blend tree),
  `Notice`, `Death` + `DeathIndex`; `CombatAnimatorBridge` the attacks, `Block`, `Dodge`, `HitReact` and by tier
  `HitLight`/`HitHeavy`/`Knockdown`. Missing parameters are skipped.
- **Why it did that:** events `EnemyNoticedEvent`, `EnemyStateChangedEvent` (with the reason),
  `AttackTokenGrantedEvent`/`AttackTokenReleasedEvent`, `AttackChosenEvent`, `EnemyDiedEvent`, and `Decision`
  entries in the timeline ("Circle -> Attacking: slash", "token released: combo missed").

Outside the kit (a training dummy), `arena.AddEnemyTarget(id)` makes something a target; `CombatEnemyAI.UseBehaviour`
sets a behaviour from code. Path finding is not built in: the enemies steer straight (with separation); a level with
walls between them and the player needs a pathfinding layer under `CombatEnemyAI` (NavMesh, or `DistanceMap`).

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
