using System;
using System.Collections.Generic;
using RPGFramework.Combat;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// The rules of real-time action combat, without the engine. Attacks run in phases (wind-up, active, recovery;
    /// optionally charged first); a hit is decided by contact: the game's hitboxes report what they touch
    /// (<see cref="ReportContact"/>) and a contact lands only inside the attacker's active window, once per target per
    /// swing. A landed blow deals damage through <see cref="ICombatDamage"/> and may stagger the target; a dodge is
    /// invulnerable for its own time (or a window inside it); a held block stops blows from the front at a cost.
    /// Time moves only by <see cref="Tick"/>. Everything is published as events and recorded in the
    /// <see cref="Timeline"/>.
    /// </summary>
    public sealed class CombatRules
    {
        private readonly Dictionary<int, Fighter> _fighters = new Dictionary<int, Fighter>();
        private readonly List<Fighter> _order = new List<Fighter>();
        private readonly Dictionary<int, Swing> _swings = new Dictionary<int, Swing>();
        private readonly List<Swing> _expired = new List<Swing>();
        private Fighter[] _ticking = Array.Empty<Fighter>();
        private ICombatDamage _damage;
        private int _lastSwing;

        /// <param name="damage">Deals the damage of landed blows.</param>
        /// <param name="events">Where the combat events go (null: none).</param>
        /// <param name="timeline">Where the fight is recorded (null: not recorded).</param>
        public CombatRules(ICombatDamage damage, IEventPublisher events = null, CombatTimeline timeline = null)
        {
            Damage = damage;
            Events = events;
            Timeline = timeline;
        }

        public ICombatDamage Damage
        {
            get => _damage;
            set => _damage = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// What a block costs per blow (stamina); null: blocking is free.
        /// </summary>
        public IBlockStock BlockStock { get; set; }

        public IEventPublisher Events { get; }
        public CombatTimeline Timeline { get; }

        /// <summary>
        /// Seconds of combat so far (the sum of every <see cref="Tick"/>).
        /// </summary>
        public float Time { get; private set; }

        /// <summary>
        /// Takes part in the fight. Again for one already in: its settings are replaced.
        /// </summary>
        /// <returns>False when it was already in.</returns>
        public bool Add(int entityId, CombatantSettings settings = null)
        {
            settings = settings ?? new CombatantSettings();
            if (_fighters.TryGetValue(entityId, out var known))
            {
                known.Settings = settings;
                return false;
            }
            var fighter = new Fighter(entityId, settings) { PoiseLeft = settings.Poise };
            _fighters.Add(entityId, fighter);
            _order.Add(fighter);
            return true;
        }

        /// <summary>
        /// Leaves the fight (died, despawned). An attack it was making ends; its flying projectiles fly on.
        /// </summary>
        public bool Remove(int entityId)
        {
            if (!_fighters.TryGetValue(entityId, out var fighter))
                return false;
            if (IsAttacking(fighter.State))
                EndAttack(fighter, true, Time);
            else if (fighter.State == CombatState.Dodging)
                Raise(new DodgeEndedEvent(entityId, true, Time), new TimelineEntry(Time, TimelineKind.DodgeEnded, entityId, note: "left"));
            DropMeleeSwing(fighter);
            fighter.Removed = true;
            _fighters.Remove(entityId);
            _order.Remove(fighter);
            return true;
        }

        public bool Contains(int entityId) => _fighters.ContainsKey(entityId);

        public IEnumerable<int> Combatants
        {
            get
            {
                foreach (var fighter in _order)
                    yield return fighter.Id;
            }
        }

        public CombatantSettings SettingsOf(int entityId) => Find(entityId)?.Settings;

        /// <summary>
        /// What the combatant is doing (Idle for one not in the fight).
        /// </summary>
        public CombatState StateOf(int entityId) => Find(entityId)?.State ?? CombatState.Idle;

        /// <summary>
        /// The attack it is charging or making, or null.
        /// </summary>
        public AttackDefinition AttackOf(int entityId)
        {
            var fighter = Find(entityId);
            return fighter != null && IsAttacking(fighter.State) ? fighter.Attack : null;
        }

        /// <summary>
        /// The swing of the attack it is charging or making (what its hitboxes report), or 0.
        /// </summary>
        public int SwingOf(int entityId)
        {
            var fighter = Find(entityId);
            return fighter != null && IsAttacking(fighter.State) ? fighter.Swing : 0;
        }

        /// <summary>
        /// Seconds into the current phase (an attack's phase, a dodge, a stagger; a charge's hold).
        /// </summary>
        public float PhaseElapsed(int entityId) => Find(entityId)?.Elapsed ?? 0f;

        /// <summary>
        /// Seconds the current phase lasts (0 for Idle, Blocking and Charging, which last until something ends them).
        /// </summary>
        public float PhaseLength(int entityId) => Find(entityId)?.Length ?? 0f;

        /// <summary>
        /// Seconds the current charge has been held (0 when not charging).
        /// </summary>
        public float ChargeHeld(int entityId)
        {
            var fighter = Find(entityId);
            return fighter != null && fighter.State == CombatState.Charging ? fighter.Elapsed : 0f;
        }

        /// <summary>
        /// Inside a dodge's invulnerability.
        /// </summary>
        public bool IsInvulnerable(int entityId)
        {
            var fighter = Find(entityId);
            return fighter != null && IsInvulnerable(fighter);
        }

        /// <summary>
        /// The block button is held (it blocks only when <see cref="StateOf"/> is Blocking).
        /// </summary>
        public bool IsBlockHeld(int entityId) => Find(entityId)?.BlockHeld ?? false;

        /// <summary>
        /// The swing can land now (its active window is open).
        /// </summary>
        public bool IsSwingLive(int swingId) => _swings.TryGetValue(swingId, out var swing) && swing.Live;

        /// <summary>
        /// Where the combatant stands and faces (for the block's cone). The Unity layer sets it every frame.
        /// </summary>
        public void SetPose(int entityId, CombatVector position, CombatVector forward)
        {
            var fighter = Find(entityId);
            if (fighter == null)
                return;
            fighter.Position = position;
            fighter.Forward = forward;
            fighter.HasPose = true;
        }

        /// <summary>
        /// Starts an attack (its wind-up). Only from Idle or Blocking (the block waits until the attack is over).
        /// </summary>
        /// <returns>False when refused (recorded in the timeline).</returns>
        public bool Attack(int entityId, AttackDefinition attack)
        {
            if (attack == null)
                throw new ArgumentNullException(nameof(attack));
            return StartAttack(entityId, attack, charging: false);
        }

        /// <summary>
        /// Starts holding a chargeable attack (<see cref="AttackDefinition.CanCharge"/>): a heavy swing, a bow's draw.
        /// It holds until <see cref="Release"/>.
        /// </summary>
        public bool BeginCharge(int entityId, AttackDefinition attack)
        {
            if (attack == null)
                throw new ArgumentNullException(nameof(attack));
            if (!attack.CanCharge)
                throw new ArgumentException($"Attack '{attack.Id}' cannot be charged (MaxCharge is 0)", nameof(attack));
            return StartAttack(entityId, attack, charging: true);
        }

        /// <summary>
        /// Lets a charge go: its wind-up starts, its damage multiplied by the charge (clamped to
        /// [MinCharge, MaxCharge]); under MinCharge it is the quick attack.
        /// </summary>
        /// <returns>False when it was not charging.</returns>
        public bool Release(int entityId)
        {
            var fighter = Find(entityId);
            if (fighter == null || fighter.State != CombatState.Charging)
                return Refuse(fighter, entityId, "release");

            var charged = fighter.Attack;
            float held = fighter.Elapsed;
            bool quick = held < charged.MinCharge;
            var played = quick ? charged.QuickAttack ?? charged : charged;
            float multiplier = quick ? 1f : charged.ChargeMultiplier(held);
            float counted = Math.Min(held, charged.MaxCharge);

            var swing = _swings[fighter.Swing];
            swing.Attack = played;
            swing.Multiplier = multiplier;
            fighter.Attack = played;
            Raise(new AttackReleasedEvent(entityId, fighter.Swing, played.Id, counted, multiplier, quick, Time),
                new TimelineEntry(Time, TimelineKind.AttackReleased, entityId, swingId: fighter.Swing, attackId: played.Id,
                    value: multiplier, note: quick ? $"quick after {held:0.###}s" : $"held {counted:0.###}s"));
            Enter(fighter, CombatState.WindUp, played.WindUp);
            Advance(fighter, 0f);
            return true;
        }

        /// <summary>
        /// Dodges in a direction: invulnerable for the dodge (or its window, <see cref="CombatantSettings"/>). From
        /// Idle, Blocking, Charging (the charge is dropped) or Recovery (cut short); not in the middle of a blow or a
        /// stagger.
        /// </summary>
        public bool Dodge(int entityId, CombatVector direction)
        {
            var fighter = Find(entityId);
            if (fighter == null)
                return Refuse(null, entityId, "dodge");
            switch (fighter.State)
            {
                case CombatState.Idle:
                    break;
                case CombatState.Blocking:
                    Raise(new BlockLoweredEvent(entityId, Time), new TimelineEntry(Time, TimelineKind.BlockLowered, entityId));
                    break;
                case CombatState.Charging:
                case CombatState.Recovery:
                    EndAttack(fighter, true, Time);
                    break;
                default:
                    return Refuse(fighter, entityId, "dodge");
            }

            var settings = fighter.Settings;
            fighter.DodgeDirection = direction;
            Enter(fighter, CombatState.Dodging, settings.DodgeSeconds);
            float from = Time + Math.Min(settings.InvulnerableFrom, settings.DodgeSeconds);
            float until = Time + settings.InvulnerableEnd;
            Raise(new DodgeStartedEvent(entityId, direction, settings.DodgeSeconds, from, until, Time),
                new TimelineEntry(Time, TimelineKind.DodgeStarted, entityId, value: settings.DodgeSeconds,
                    note: $"invulnerable {from:0.###}-{until:0.###}"));
            Advance(fighter, 0f);
            return true;
        }

        /// <summary>
        /// Holds or lets go of the block. Held while busy (attacking, dodging, staggered), it rises once that ends.
        /// </summary>
        public void SetBlocking(int entityId, bool held)
        {
            var fighter = Find(entityId);
            if (fighter == null)
                return;
            fighter.BlockHeld = held;
            if (held && fighter.State == CombatState.Idle)
            {
                fighter.State = CombatState.Blocking;
                Raise(new BlockRaisedEvent(entityId, Time), new TimelineEntry(Time, TimelineKind.BlockRaised, entityId));
            }
            else if (!held && fighter.State == CombatState.Blocking)
            {
                fighter.State = CombatState.Idle;
                Raise(new BlockLoweredEvent(entityId, Time), new TimelineEntry(Time, TimelineKind.BlockLowered, entityId));
            }
        }

        /// <summary>
        /// A hitbox of <paramref name="attackerId"/>'s swing touched <paramref name="targetId"/>'s body. It lands only
        /// in the swing's active window and once per target per swing; then it is dodged (invulnerable), blocked (held
        /// block, facing the attacker, paid for), or a hit: damage through <see cref="Damage"/> and maybe a stagger.
        /// A target that is not in the fight (a crate, a plain enemy) just takes the hit.
        /// </summary>
        public ContactResult ReportContact(int attackerId, int targetId, int swingId, CombatVector point = default)
        {
            _swings.TryGetValue(swingId, out var swing);
            if (swing == null || swing.AttackerId != attackerId)
                return Resolve(attackerId, targetId, swingId, null, ContactResult.Ignored, ContactIgnored.UnknownSwing, 0f, false, point);
            string attackId = swing.Attack.Id;
            if (targetId == attackerId)
                return Resolve(attackerId, targetId, swingId, attackId, ContactResult.Ignored, ContactIgnored.Self, 0f, false, point);
            if (!swing.Live)
                return Resolve(attackerId, targetId, swingId, attackId, ContactResult.Ignored, ContactIgnored.OutsideActiveWindow, 0f, false, point);
            if (swing.Met.Contains(targetId))
                return Resolve(attackerId, targetId, swingId, attackId, ContactResult.Ignored, ContactIgnored.AlreadyHit, 0f, false, point);
            if (!Damage.CanHit(attackerId, targetId))
                return Resolve(attackerId, targetId, swingId, attackId, ContactResult.Ignored, ContactIgnored.NotHostile, 0f, false, point);

            swing.Met.Add(targetId);
            var target = Find(targetId);
            if (target != null && IsInvulnerable(target))
                return Resolve(attackerId, targetId, swingId, attackId, ContactResult.Dodged, ContactIgnored.None, 0f, false, point);

            bool guardBroken = false;
            if (target != null && target.State == CombatState.Blocking && Faces(target, attackerId, point))
            {
                if (BlockStock == null || BlockStock.TrySpend(targetId, swing.Attack.BlockCost))
                {
                    float through = target.Settings.BlockDamageMultiplier;
                    float blocked = through > 0f ? Deal(swing, targetId, through) : 0f;
                    return Resolve(attackerId, targetId, swingId, attackId, ContactResult.Blocked, ContactIgnored.None, blocked, false, point);
                }
                guardBroken = true;
            }

            float dealt = Deal(swing, targetId, 1f);
            var result = Resolve(attackerId, targetId, swingId, attackId, ContactResult.Hit, ContactIgnored.None, dealt, guardBroken, point);
            if (target != null && !target.Removed)
                React(target, attackerId, swing.Attack, guardBroken);
            return result;
        }

        /// <summary>
        /// A projectile's swing is over (it hit and stopped, hit a wall, or fell): later contacts of it are ignored.
        /// </summary>
        public bool EndSwing(int swingId)
        {
            if (!_swings.TryGetValue(swingId, out var swing) || !swing.Projectile)
                return false;
            EndProjectile(swing, Time, "ended");
            return true;
        }

        /// <summary>
        /// Moves the fight on: phases end and the next begin (several in one tick when it is long), projectiles run
        /// out of time.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Time moves forward");
            Time += deltaSeconds;

            if (_ticking.Length != _order.Count)
                _ticking = new Fighter[_order.Count];
            _order.CopyTo(_ticking);
            foreach (var fighter in _ticking)
            {
                if (!fighter.Removed)
                    Advance(fighter, deltaSeconds);
            }

            _expired.Clear();
            foreach (var swing in _swings.Values)
            {
                if (swing.Projectile && swing.ExpiresAt <= Time)
                    _expired.Add(swing);
            }
            foreach (var swing in _expired)
                EndProjectile(swing, swing.ExpiresAt, "lifetime");
        }

        private bool StartAttack(int entityId, AttackDefinition attack, bool charging)
        {
            var fighter = Find(entityId);
            if (fighter == null || (fighter.State != CombatState.Idle && fighter.State != CombatState.Blocking))
                return Refuse(fighter, entityId, (charging ? "charge " : "attack ") + attack.Id);
            if (fighter.State == CombatState.Blocking)
                Raise(new BlockLoweredEvent(entityId, Time), new TimelineEntry(Time, TimelineKind.BlockLowered, entityId));

            DropMeleeSwing(fighter);
            var swing = new Swing(++_lastSwing, entityId, attack);
            _swings.Add(swing.Id, swing);
            fighter.Swing = swing.Id;
            fighter.Attack = attack;

            Enter(fighter, charging ? CombatState.Charging : CombatState.WindUp, charging ? 0f : attack.WindUp);
            Raise(new AttackStartedEvent(entityId, swing.Id, attack.Id, charging, Time),
                new TimelineEntry(Time, charging ? TimelineKind.ChargeStarted : TimelineKind.AttackStarted, entityId,
                    swingId: swing.Id, attackId: attack.Id));
            Advance(fighter, 0f);
            return true;
        }

        // Runs the fighter's clock by `left` seconds of the current tick (the tick ends at Time)
        private void Advance(Fighter fighter, float left)
        {
            while (!fighter.Removed)
            {
                switch (fighter.State)
                {
                    case CombatState.Idle:
                    case CombatState.Blocking:
                        return;
                    case CombatState.Charging:
                        fighter.Elapsed += left;
                        return;
                }

                float need = fighter.Length - fighter.Elapsed;
                if (left < need)
                {
                    fighter.Elapsed += left;
                    return;
                }
                left -= need;
                fighter.Elapsed = fighter.Length;
                EndPhase(fighter, Time - left);
            }
        }

        private void EndPhase(Fighter fighter, float at)
        {
            int id = fighter.Id;
            var attack = fighter.Attack;
            switch (fighter.State)
            {
                case CombatState.WindUp:
                {
                    var swing = _swings[fighter.Swing];
                    swing.Live = true;
                    if (attack.Projectile)
                    {
                        swing.Projectile = true;
                        swing.ExpiresAt = at + attack.ProjectileLifetime;
                    }
                    Enter(fighter, CombatState.Active, attack.Active);
                    Raise(new AttackActiveStartedEvent(id, swing.Id, attack.Id, attack.Projectile, at),
                        new TimelineEntry(at, TimelineKind.ActiveStarted, id, swingId: swing.Id, attackId: attack.Id,
                            note: attack.Projectile ? "projectile" : null));
                    break;
                }
                case CombatState.Active:
                    CloseActive(fighter, at);
                    Enter(fighter, CombatState.Recovery, attack.Recovery);
                    break;
                case CombatState.Recovery:
                    EndAttack(fighter, false, at);
                    Rest(fighter, at);
                    break;
                case CombatState.Dodging:
                    Raise(new DodgeEndedEvent(id, false, at), new TimelineEntry(at, TimelineKind.DodgeEnded, id));
                    Rest(fighter, at);
                    break;
                case CombatState.HitReact:
                    Raise(new HitReactEndedEvent(id, at), new TimelineEntry(at, TimelineKind.HitReactEnded, id));
                    Rest(fighter, at);
                    break;
            }
        }

        private void CloseActive(Fighter fighter, float at)
        {
            if (_swings.TryGetValue(fighter.Swing, out var swing) && !swing.Projectile)
                swing.Live = false;
            Raise(new AttackActiveEndedEvent(fighter.Id, fighter.Swing, fighter.Attack.Id, at),
                new TimelineEntry(at, TimelineKind.ActiveEnded, fighter.Id, swingId: fighter.Swing, attackId: fighter.Attack.Id));
        }

        private void EndAttack(Fighter fighter, bool interrupted, float at)
        {
            if (fighter.State == CombatState.Active)
                CloseActive(fighter, at);
            if (_swings.TryGetValue(fighter.Swing, out var swing) && !swing.Projectile)
                swing.Live = false;
            var attack = fighter.Attack;
            Raise(new AttackEndedEvent(fighter.Id, fighter.Swing, attack.Id, interrupted, at),
                new TimelineEntry(at, TimelineKind.AttackEnded, fighter.Id, swingId: fighter.Swing, attackId: attack.Id,
                    note: interrupted ? "interrupted" : null));
            fighter.Attack = null;
        }

        private void Rest(Fighter fighter, float at)
        {
            fighter.Attack = null;
            Enter(fighter, fighter.BlockHeld ? CombatState.Blocking : CombatState.Idle, 0f);
            if (fighter.State == CombatState.Blocking)
                Raise(new BlockRaisedEvent(fighter.Id, at), new TimelineEntry(at, TimelineKind.BlockRaised, fighter.Id));
        }

        private void React(Fighter target, int attackerId, AttackDefinition attack, bool force)
        {
            var settings = target.Settings;
            if (settings.HitReactSeconds <= 0f)
                return;
            if (!force)
            {
                if (attack.Stagger <= 0f)
                    return;
                // Hyper armour: the blow hurts but does not stop this attack
                if (target.Attack != null && !target.Attack.Interruptible &&
                    (target.State == CombatState.Charging || target.State == CombatState.WindUp || target.State == CombatState.Active))
                    return;
                if (settings.Poise > 0f)
                {
                    if (Time - target.LastHitAt >= settings.PoiseRecoverySeconds)
                        target.PoiseLeft = settings.Poise;
                    target.LastHitAt = Time;
                    target.PoiseLeft -= attack.Stagger;
                    if (target.PoiseLeft > 0f)
                        return;
                    target.PoiseLeft = settings.Poise;
                }
            }

            switch (target.State)
            {
                case CombatState.Charging:
                case CombatState.WindUp:
                case CombatState.Active:
                case CombatState.Recovery:
                    EndAttack(target, true, Time);
                    break;
                case CombatState.Dodging:
                    Raise(new DodgeEndedEvent(target.Id, true, Time),
                        new TimelineEntry(Time, TimelineKind.DodgeEnded, target.Id, note: "interrupted"));
                    break;
                case CombatState.Blocking:
                    Raise(new BlockLoweredEvent(target.Id, Time), new TimelineEntry(Time, TimelineKind.BlockLowered, target.Id));
                    break;
            }
            Enter(target, CombatState.HitReact, settings.HitReactSeconds);
            Raise(new HitReactStartedEvent(target.Id, attackerId, settings.HitReactSeconds, Time),
                new TimelineEntry(Time, TimelineKind.HitReactStarted, target.Id, attackerId, value: settings.HitReactSeconds,
                    note: force ? "guard broken" : null));
        }

        private float Deal(Swing swing, int targetId, float share)
        {
            var attack = swing.Attack;
            var context = new AttackContext(attack.Tags, attack.SourceId)
                .WithMultiplier(attack.DamageMultiplier * swing.Multiplier * share);
            return Damage.Deal(swing.AttackerId, targetId, context);
        }

        private bool Faces(Fighter defender, int attackerId, CombatVector point)
        {
            if (!defender.HasPose)
                return true;
            var attacker = Find(attackerId);
            CombatVector from;
            if (attacker != null && attacker.HasPose)
                from = attacker.Position;
            else if (!point.Equals(CombatVector.Zero))
                from = point;
            else
                return true;
            var toAttacker = (from - defender.Position).Flat;
            var forward = defender.Forward.Flat;
            if (toAttacker.Length < 1e-4f || forward.Length < 1e-4f)
                return true;
            return CombatVector.Angle(forward, toAttacker) <= defender.Settings.BlockAngle * 0.5f;
        }

        private ContactResult Resolve(int attackerId, int targetId, int swingId, string attackId, ContactResult result,
            ContactIgnored ignored, float damage, bool guardBroken, CombatVector point)
        {
            Raise(new ContactResolvedEvent(attackerId, targetId, swingId, attackId, result, ignored, damage, guardBroken, point, Time),
                new TimelineEntry(Time, TimelineKind.Contact, attackerId, targetId, swingId, attackId, result, damage,
                    ignored != ContactIgnored.None ? ignored.ToString() : guardBroken ? "guard broken" : null));
            return result;
        }

        private void EndProjectile(Swing swing, float at, string why)
        {
            swing.Live = false;
            _swings.Remove(swing.Id);
            Timeline?.Record(new TimelineEntry(at, TimelineKind.SwingEnded, swing.AttackerId, swingId: swing.Id,
                attackId: swing.Attack.Id, note: why));
        }

        // A melee swing is kept after its window (so late contacts read "outside the window") until the next one
        private void DropMeleeSwing(Fighter fighter)
        {
            if (fighter.Swing != 0 && _swings.TryGetValue(fighter.Swing, out var swing) && !swing.Projectile)
                _swings.Remove(swing.Id);
            fighter.Swing = 0;
        }

        private bool Refuse(Fighter fighter, int entityId, string action)
        {
            Timeline?.Record(new TimelineEntry(Time, TimelineKind.Refused, entityId,
                note: fighter == null ? $"{action}: not in the fight" : $"{action} while {fighter.State}"));
            return false;
        }

        private void Raise<TEvent>(TEvent combatEvent, TimelineEntry entry) where TEvent : IEvent
        {
            Timeline?.Record(entry);
            Events?.Publish(combatEvent);
        }

        private static void Enter(Fighter fighter, CombatState state, float length)
        {
            fighter.State = state;
            fighter.Elapsed = 0f;
            fighter.Length = length;
        }

        private static bool IsInvulnerable(Fighter fighter) =>
            fighter.State == CombatState.Dodging &&
            fighter.Elapsed >= fighter.Settings.InvulnerableFrom &&
            fighter.Elapsed < fighter.Settings.InvulnerableEnd;

        private static bool IsAttacking(CombatState state) =>
            state == CombatState.Charging || state == CombatState.WindUp || state == CombatState.Active || state == CombatState.Recovery;

        private Fighter Find(int entityId) => _fighters.TryGetValue(entityId, out var fighter) ? fighter : null;

        private sealed class Fighter
        {
            public Fighter(int id, CombatantSettings settings)
            {
                Id = id;
                Settings = settings;
            }

            public int Id { get; }
            public CombatantSettings Settings;
            public CombatState State;
            public float Elapsed;
            public float Length;
            public AttackDefinition Attack;
            public int Swing;
            public bool BlockHeld;
            public float PoiseLeft;
            public float LastHitAt = float.NegativeInfinity;
            public CombatVector Position;
            public CombatVector Forward;
            public bool HasPose;
            public CombatVector DodgeDirection;
            public bool Removed;
        }

        private sealed class Swing
        {
            public Swing(int id, int attackerId, AttackDefinition attack)
            {
                Id = id;
                AttackerId = attackerId;
                Attack = attack;
            }

            public int Id { get; }
            public int AttackerId { get; }
            public AttackDefinition Attack;
            public float Multiplier = 1f;
            public bool Live;
            public bool Projectile;
            public float ExpiresAt;
            public readonly HashSet<int> Met = new HashSet<int>();
        }
    }
}
