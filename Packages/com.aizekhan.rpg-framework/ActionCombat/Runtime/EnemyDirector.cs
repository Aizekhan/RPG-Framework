using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// The combat AI of action combat's enemies, without the engine. Each tick every enemy notices (sight radius, view
    /// cone, line of sight, hearing, being struck; it remembers a lost target for a while, then goes home), moves
    /// (patrol, chase, approach, circle, back off; separated from the others) and fights: at most
    /// <see cref="AttackTokens.MaxPerTarget"/> enemies attack one target at once, the others circle; an enemy with a
    /// token picks a move by distance, cooldowns and weights (seeded), chains combos while they land, then pauses (the
    /// player's punish window). It may block or dodge a swing aimed at it. The body follows its
    /// <see cref="EnemyIntent"/>; the attacks, stagger and dodges go through <see cref="CombatRules"/>. Every decision is
    /// an event and a timeline entry, so a panel can show why a monster did what it did.
    /// </summary>
    public sealed class EnemyDirector : IDisposable
    {
        private readonly CombatRules _rules;
        private readonly IEnemyWorld _world;
        private readonly IEventBus _events;
        private readonly Random _random;
        private readonly Dictionary<int, Brain> _brains = new Dictionary<int, Brain>();
        private readonly List<Brain> _order = new List<Brain>();
        private readonly List<int> _targets = new List<int>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <param name="events">The arena's bus: decisions are published there, and the director reads the combat
        /// events (contacts, swings) from it. Null: no events, no reactions to swings.</param>
        /// <param name="seed">Seeds every roll (moves, defences, wandering) so a fight can be replayed.</param>
        public EnemyDirector(CombatRules rules, IEnemyWorld world, IEventBus events = null, int seed = 0, AttackTokens tokens = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _events = events;
            _random = new Random(seed);
            Tokens = tokens ?? new AttackTokens();
            if (events != null)
            {
                _subscriptions.Add(events.Subscribe<ContactResolvedEvent>(OnContact));
                _subscriptions.Add(events.Subscribe<AttackStartedEvent>(OnSwingStarted));
            }
        }

        public AttackTokens Tokens { get; }

        /// <summary>
        /// The rules' clock.
        /// </summary>
        public float Time => _rules.Time;

        public IEnumerable<int> Enemies
        {
            get
            {
                foreach (var brain in _order)
                    yield return brain.Id;
            }
        }

        /// <summary>
        /// Starts driving an enemy (it should also be in the rules: <see cref="CombatRules.Add"/>).
        /// </summary>
        /// <param name="home">Where it stands, patrols around and goes back to.</param>
        public void Add(int enemyId, EnemyBehaviour behaviour, CombatVector home)
        {
            if (behaviour == null)
                throw new ArgumentNullException(nameof(behaviour));
            Remove(enemyId);
            var brain = new Brain(enemyId, behaviour, home) { CircleSign = _random.Next(2) == 0 ? -1f : 1f };
            brain.State = behaviour.PatrolPoints.Count > 0 || behaviour.WanderRadius > 0f ? EnemyState.Patrol : EnemyState.Idle;
            _brains.Add(enemyId, brain);
            _order.Add(brain);
        }

        public bool Remove(int enemyId)
        {
            if (!_brains.TryGetValue(enemyId, out var brain))
                return false;
            ReleaseToken(brain, "removed");
            _brains.Remove(enemyId);
            _order.Remove(brain);
            return true;
        }

        public bool Contains(int enemyId) => _brains.ContainsKey(enemyId);

        public EnemyState StateOf(int enemyId) => Find(enemyId)?.State ?? EnemyState.Idle;

        /// <summary>
        /// What the body should do now (move, face, how fast it may turn).
        /// </summary>
        public EnemyIntent IntentOf(int enemyId) => Find(enemyId)?.Intent ?? EnemyIntent.Still(EnemyState.Idle, -1);

        /// <summary>
        /// Who it fights, or -1.
        /// </summary>
        public int TargetOf(int enemyId) => Find(enemyId)?.Target ?? -1;

        public EnemyBehaviour BehaviourOf(int enemyId) => Find(enemyId)?.Behaviour;

        /// <summary>
        /// Makes it fight <paramref name="targetId"/> at once, without the Notice pause (it was struck or heard a noise).
        /// </summary>
        public void Alert(int enemyId, int targetId)
        {
            var brain = Find(enemyId);
            if (brain == null || brain.State == EnemyState.Dead || targetId < 0 || targetId == enemyId)
                return;
            if (brain.Target == targetId)
            {
                brain.LastSeenAt = Time;
                return;
            }
            if (brain.Target >= 0)
                return;
            Engage(brain, targetId, struck: true);
        }

        /// <summary>
        /// It died: it stops, gives up its token and plays a death clip (<see cref="EnemyDiedEvent.Variant"/>).
        /// </summary>
        public void Kill(int enemyId)
        {
            var brain = Find(enemyId);
            if (brain == null || brain.State == EnemyState.Dead)
                return;
            ReleaseToken(brain, "died");
            if (brain.Blocking)
                _rules.SetBlocking(enemyId, false);
            int variant = _random.Next(Math.Max(1, brain.Behaviour.DeathVariants));
            Set(brain, EnemyState.Dead, "died");
            brain.Intent = EnemyIntent.Still(EnemyState.Dead, -1);
            _events?.Publish(new EnemyDiedEvent(enemyId, variant, Time));
        }

        /// <summary>
        /// Decides for every enemy (call after <see cref="CombatRules.Tick"/>), then hands out the attack tokens.
        /// </summary>
        public void Tick()
        {
            foreach (var brain in _order.ToArray())
            {
                if (brain.State == EnemyState.Dead || !_brains.ContainsKey(brain.Id))
                    continue;
                if (!_world.IsAlive(brain.Id))
                {
                    Kill(brain.Id);
                    continue;
                }
                Think(brain);
            }

            foreach (var (enemy, target) in Tokens.Grant())
            {
                var brain = Find(enemy);
                if (brain != null)
                    brain.WantSince = -1f;
                Note(enemy, target, "token granted");
                _events?.Publish(new AttackTokenGrantedEvent(enemy, target, Time));
            }
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
        }

        private void Think(Brain brain)
        {
            int id = brain.Id;
            var behaviour = brain.Behaviour;
            if (!_world.TryGetPose(id, out var position, out var forward))
            {
                brain.Intent = EnemyIntent.Still(brain.State, brain.Target);
                return;
            }
            brain.Position = position;
            Perceive(brain, position, forward);

            var combat = _rules.StateOf(id);
            if (combat == CombatState.HitReact)
            {
                if (brain.Attacking)
                    EndAttacks(brain, "interrupted", pause: false);
                Set(brain, EnemyState.Reacting, _rules.HitTierOf(id) + " hit");
                brain.Intent = EnemyIntent.Still(EnemyState.Reacting, brain.Target);
                return;
            }

            if (brain.Attacking)
            {
                if (combat == CombatState.Charging || combat == CombatState.WindUp || combat == CombatState.Active ||
                    combat == CombatState.Recovery)
                {
                    float turn = combat == CombatState.WindUp && brain.Move.TrackDuringWindUp ? behaviour.TurnSpeed : 0f;
                    brain.Intent = new EnemyIntent(EnemyState.Attacking, brain.Target, CombatVector.Zero, 0f,
                        ToTarget(brain, position), turn);
                    return;
                }
                brain.Attacking = false;
                var next = brain.Move.Next;
                if (next != null && brain.ComboLanded && TargetDistance(brain, position, out float distance) &&
                    next.InRange(distance) && Ready(brain, next) && StartMove(brain, next, distance, combo: true))
                    return;
                EndAttacks(brain, next != null && !brain.ComboLanded ? "combo missed" : "attack over", pause: true);
            }

            if (brain.Blocking && (Time >= brain.BlockUntil || combat != CombatState.Blocking))
            {
                _rules.SetBlocking(id, false);
                brain.Blocking = false;
            }
            if (combat == CombatState.Dodging || brain.Blocking)
            {
                Set(brain, EnemyState.Defending, combat == CombatState.Dodging ? "dodging" : "blocking");
                brain.Intent = new EnemyIntent(EnemyState.Defending, brain.Target, CombatVector.Zero, 0f,
                    ToTarget(brain, position), behaviour.TurnSpeed);
                return;
            }

            if (brain.Target < 0)
            {
                Wander(brain, position);
                return;
            }

            var face = ToTarget(brain, position);
            if (brain.State == EnemyState.Notice && Time < brain.NoticeUntil)
            {
                brain.Intent = new EnemyIntent(EnemyState.Notice, brain.Target, CombatVector.Zero, 0f, face, behaviour.TurnSpeed);
                return;
            }
            if (Time < brain.RecoverUntil)
            {
                brain.Intent = new EnemyIntent(EnemyState.Recovering, brain.Target, CombatVector.Zero, 0f, face, behaviour.TurnSpeed);
                return;
            }
            if (!TargetDistance(brain, position, out float d))
            {
                brain.Intent = EnemyIntent.Still(brain.State, brain.Target);
                return;
            }

            if (Time < brain.BackOffUntil)
            {
                Go(brain, EnemyState.BackOff, "hurt", Away(face), behaviour.BackOffSpeed, face);
                return;
            }
            if (behaviour.BackOffHealthShare > 0f && _world.HealthShare(id) < behaviour.BackOffHealthShare &&
                Time >= brain.BackOffRolledAt + Math.Max(1f, behaviour.BackOffSeconds))
            {
                brain.BackOffRolledAt = Time;
                if (_random.NextDouble() < behaviour.BackOffChance)
                {
                    brain.BackOffUntil = Time + behaviour.BackOffSeconds;
                    ReleaseToken(brain, "backing off hurt");
                    Go(brain, EnemyState.BackOff, "hurt", Away(face), behaviour.BackOffSpeed, face);
                    return;
                }
            }

            if (Tokens.Holds(id))
            {
                var move = Choose(brain, d);
                if (move != null && StartMove(brain, move, d, combo: false))
                    return;
                float reach = Reach(brain);
                if (reach > 0f && d > reach)
                {
                    Go(brain, d > behaviour.EngageDistance ? EnemyState.Chase : EnemyState.Approach, "closing in to strike", face,
                        d > behaviour.EngageDistance ? behaviour.ChaseSpeed : behaviour.ApproachSpeed, face);
                }
                else if (reach > 0f && d < Closest(brain))
                {
                    Go(brain, EnemyState.BackOff, "too close for its moves", Away(face), behaviour.BackOffSpeed, face);
                }
                else
                {
                    // In reach but nothing to use now (cooldowns, a gap between ranges, no usable move): give the turn to
                    // another and ask again when a move is ready
                    ReleaseToken(brain, reach > 0f ? "no move ready" : "no usable move");
                    brain.WantAfter = reach > 0f ? Math.Max(NextReady(brain), Time + 0.25f) : Time + 1f;
                    Circle(brain, d, face);
                }
                return;
            }

            // In fighting range it asks for its turn: nearer and longer waiting go first
            if (d <= behaviour.EngageDistance && Time >= brain.WantAfter)
            {
                if (brain.WantSince < 0f)
                    brain.WantSince = Time;
                Tokens.Want(id, brain.Target, Time - brain.WantSince - d);
            }

            if (d > behaviour.EngageDistance)
                Go(brain, EnemyState.Chase, "target far", face, behaviour.ChaseSpeed, face);
            else if (d < behaviour.BackOffDistance)
                Go(brain, EnemyState.BackOff, "too close while waiting", Away(face), behaviour.BackOffSpeed, face);
            else
                Circle(brain, d, face);
        }

        private void Perceive(Brain brain, CombatVector position, CombatVector forward)
        {
            var behaviour = brain.Behaviour;
            _targets.Clear();
            _targets.AddRange(_world.TargetsOf(brain.Id));

            if (brain.Target >= 0)
            {
                if (!_targets.Contains(brain.Target) || !_world.IsAlive(brain.Target))
                {
                    Lose(brain, "target gone");
                }
                else
                {
                    if (Sees(brain, brain.Target, position, forward))
                        brain.LastSeenAt = Time;
                    if (Time - brain.LastSeenAt > behaviour.MemorySeconds)
                        Lose(brain, "lost sight");
                    else if (behaviour.LeashRadius > 0f && (position - brain.Home).Flat.Length > behaviour.LeashRadius)
                        Lose(brain, "too far from home");
                }
            }
            if (brain.Target >= 0)
                return;

            int best = -1;
            float bestDistance = float.MaxValue;
            foreach (int target in _targets)
            {
                if (!_world.TryGetPose(target, out var where, out _) || !Sees(brain, target, position, forward))
                    continue;
                float distance = (where - position).Flat.Length;
                if (distance < bestDistance)
                {
                    best = target;
                    bestDistance = distance;
                }
            }
            if (best >= 0)
                Engage(brain, best, struck: false);
        }

        private bool Sees(Brain brain, int target, CombatVector position, CombatVector forward)
        {
            var behaviour = brain.Behaviour;
            if (!_world.TryGetPose(target, out var where, out _))
                return false;
            var toTarget = (where - position).Flat;
            float distance = toTarget.Length;
            if (distance <= behaviour.HearingRadius)
                return true;
            if (distance > behaviour.SightRadius)
                return false;
            if (forward.Flat.Length > 1e-4f && CombatVector.Angle(forward.Flat, toTarget) > behaviour.ViewAngle * 0.5f)
                return false;
            return _world.CanSee(brain.Id, target);
        }

        private void Engage(Brain brain, int target, bool struck)
        {
            brain.Target = target;
            brain.LastSeenAt = Time;
            brain.WantSince = -1f;
            Note(brain.Id, target, struck ? "struck: fights back" : "noticed");
            _events?.Publish(new EnemyNoticedEvent(brain.Id, target, struck, Time));
            if (!struck && brain.Behaviour.NoticeSeconds > 0f)
            {
                brain.NoticeUntil = Time + brain.Behaviour.NoticeSeconds;
                Set(brain, EnemyState.Notice, "noticed");
            }
            else
            {
                Set(brain, EnemyState.Chase, struck ? "struck" : "noticed");
            }
        }

        private void Lose(Brain brain, string reason)
        {
            ReleaseToken(brain, reason);
            brain.Target = -1;
            brain.WantSince = -1f;
            Set(brain, EnemyState.Returning, reason);
        }

        private void Wander(Brain brain, CombatVector position)
        {
            var behaviour = brain.Behaviour;
            if (brain.State == EnemyState.Returning)
            {
                var home = (brain.Home - position).Flat;
                if (home.Length > 0.5f)
                {
                    var way = Unit(home);
                    brain.Intent = new EnemyIntent(EnemyState.Returning, -1, Steer(brain, way), behaviour.ReturnSpeed, way, behaviour.TurnSpeed);
                    return;
                }
                brain.Goal = null;
                Set(brain, behaviour.PatrolPoints.Count > 0 || behaviour.WanderRadius > 0f ? EnemyState.Patrol : EnemyState.Idle, "home");
            }

            if (brain.State != EnemyState.Patrol)
            {
                Set(brain, EnemyState.Idle, "nothing to do");
                brain.Intent = EnemyIntent.Still(EnemyState.Idle, -1);
                return;
            }

            if (Time < brain.PauseUntil)
            {
                brain.Intent = EnemyIntent.Still(EnemyState.Patrol, -1);
                return;
            }
            if (brain.Goal == null)
            {
                if (behaviour.PatrolPoints.Count > 0)
                {
                    brain.Goal = behaviour.PatrolPoints[brain.PatrolIndex % behaviour.PatrolPoints.Count];
                }
                else
                {
                    double angle = _random.NextDouble() * Math.PI * 2.0;
                    float radius = (float)(_random.NextDouble() * behaviour.WanderRadius);
                    brain.Goal = brain.Home + new CombatVector((float)Math.Cos(angle) * radius, 0f, (float)Math.Sin(angle) * radius);
                }
            }
            var toGoal = (brain.Goal.Value - position).Flat;
            if (toGoal.Length <= 0.3f)
            {
                brain.Goal = null;
                brain.PatrolIndex++;
                brain.PauseUntil = Time + behaviour.PatrolPauseSeconds;
                brain.Intent = EnemyIntent.Still(EnemyState.Patrol, -1);
                return;
            }
            var heading = Unit(toGoal);
            brain.Intent = new EnemyIntent(EnemyState.Patrol, -1, Steer(brain, heading), behaviour.PatrolSpeed, heading, behaviour.TurnSpeed);
        }

        private void Circle(Brain brain, float distance, CombatVector face)
        {
            var behaviour = brain.Behaviour;
            if (Time >= brain.CircleSwitchAt)
            {
                brain.CircleSign = _random.Next(2) == 0 ? -1f : 1f;
                brain.CircleSwitchAt = Time + behaviour.CircleSwitchSeconds * (0.5f + (float)_random.NextDouble());
            }
            // Sideways around the target, pulled back to the circling distance
            var sideways = new CombatVector(-face.Z * brain.CircleSign, 0f, face.X * brain.CircleSign);
            float pull = Math.Max(-1f, Math.Min(1f, distance - behaviour.CircleDistance));
            var way = Unit(sideways + new CombatVector(face.X * pull, 0f, face.Z * pull));
            Go(brain, EnemyState.Circle, "waiting for its turn", way, behaviour.CircleSpeed, face);
        }

        private void Go(Brain brain, EnemyState state, string reason, CombatVector way, float speed, CombatVector face)
        {
            Set(brain, state, reason);
            brain.Intent = new EnemyIntent(state, brain.Target, Steer(brain, way), speed, face, brain.Behaviour.TurnSpeed);
        }

        // The way, pushed apart from enemies standing too close
        private CombatVector Steer(Brain brain, CombatVector way)
        {
            float space = brain.Behaviour.PersonalSpace;
            if (space <= 0f)
                return way;
            var push = CombatVector.Zero;
            foreach (var other in _order)
            {
                if (other == brain || other.State == EnemyState.Dead)
                    continue;
                var away = (brain.Position - other.Position).Flat;
                float distance = away.Length;
                if (distance < space && distance > 1e-3f)
                {
                    float strength = (1f - distance / space) / distance;
                    push = push + new CombatVector(away.X * strength, 0f, away.Z * strength);
                }
            }
            return push.Length > 0f ? Unit(way + push) : way;
        }

        private EnemyMove Choose(Brain brain, float distance)
        {
            float total = 0f;
            foreach (var move in brain.Behaviour.Moves)
            {
                if (move.InRange(distance) && Ready(brain, move))
                    total += Math.Max(0f, move.Weight);
            }
            if (total <= 0f)
                return null;
            double roll = _random.NextDouble() * total;
            EnemyMove last = null;
            foreach (var move in brain.Behaviour.Moves)
            {
                if (!move.InRange(distance) || !Ready(brain, move) || move.Weight <= 0f)
                    continue;
                last = move;
                roll -= move.Weight;
                if (roll < 0)
                    return move;
            }
            return last;
        }

        private bool StartMove(Brain brain, EnemyMove move, float distance, bool combo)
        {
            if (!_rules.Attack(brain.Id, move.Attack))
                return false;
            brain.Attacking = true;
            brain.Move = move;
            brain.Swing = _rules.SwingOf(brain.Id);
            brain.ComboLanded = false;
            brain.ReadyAt[move] = Time + move.Cooldown;
            Set(brain, EnemyState.Attacking, combo ? "combo: " + move.Attack.Id : move.Attack.Id);
            Note(brain.Id, brain.Target, $"{(combo ? "combo " : "chose ")}{move.Attack.Id} at {distance:0.0}m");
            _events?.Publish(new AttackChosenEvent(brain.Id, brain.Target, move.Attack.Id, distance, combo, Time));
            brain.Intent = new EnemyIntent(EnemyState.Attacking, brain.Target, CombatVector.Zero, 0f, CombatVector.Zero,
                move.TrackDuringWindUp ? brain.Behaviour.TurnSpeed : 0f);
            return true;
        }

        private void EndAttacks(Brain brain, string reason, bool pause)
        {
            brain.Attacking = false;
            brain.Move = null;
            ReleaseToken(brain, reason);
            if (pause && brain.Behaviour.RecoveryPause > 0f)
            {
                brain.RecoverUntil = Time + brain.Behaviour.RecoveryPause;
                Set(brain, EnemyState.Recovering, reason);
            }
        }

        private void OnContact(ContactResolvedEvent contact)
        {
            if (contact.Result == ContactResult.Ignored)
                return;
            var attacker = Find(contact.AttackerId);
            if (attacker != null && attacker.Attacking && contact.SwingId == attacker.Swing &&
                (contact.Result == ContactResult.Hit || contact.Result == ContactResult.Blocked))
                attacker.ComboLanded = true;
            if (Find(contact.TargetId) != null)
                Alert(contact.TargetId, contact.AttackerId);
        }

        // A target starts a swing: the enemies it fights may block or dodge
        private void OnSwingStarted(AttackStartedEvent started)
        {
            foreach (var brain in _order)
            {
                if (brain.Target != started.EntityId || brain.State == EnemyState.Dead || brain.Attacking || brain.Blocking)
                    continue;
                var combat = _rules.StateOf(brain.Id);
                if (combat != CombatState.Idle && combat != CombatState.Blocking)
                    continue;
                if (!_world.TryGetPose(brain.Id, out var position, out _) || !TargetDistance(brain, position, out float distance) ||
                    distance > brain.Behaviour.EngageDistance)
                    continue;

                var behaviour = brain.Behaviour;
                if (behaviour.BlockChance > 0f && _random.NextDouble() < behaviour.BlockChance)
                {
                    _rules.SetBlocking(brain.Id, true);
                    brain.Blocking = true;
                    brain.BlockUntil = Time + behaviour.BlockSeconds;
                    Note(brain.Id, started.EntityId, "blocks " + started.AttackId);
                }
                else if (behaviour.DodgeChance > 0f && _random.NextDouble() < behaviour.DodgeChance)
                {
                    var face = ToTarget(brain, position);
                    float side = _random.Next(2) == 0 ? -1f : 1f;
                    if (_rules.Dodge(brain.Id, new CombatVector(-face.Z * side, 0f, face.X * side)))
                        Note(brain.Id, started.EntityId, "dodges " + started.AttackId);
                }
            }
        }

        private void ReleaseToken(Brain brain, string reason)
        {
            int target = Tokens.TargetOf(brain.Id);
            if (!Tokens.Release(brain.Id))
                return;
            Note(brain.Id, target, "token released: " + reason);
            _events?.Publish(new AttackTokenReleasedEvent(brain.Id, target, reason, Time));
        }

        private void Set(Brain brain, EnemyState state, string reason)
        {
            if (brain.State == state)
                return;
            var from = brain.State;
            brain.State = state;
            Note(brain.Id, brain.Target, $"{from} -> {state}: {reason}");
            _events?.Publish(new EnemyStateChangedEvent(brain.Id, from, state, reason, Time));
        }

        private void Note(int enemyId, int otherId, string note) =>
            _rules.Timeline?.Record(new TimelineEntry(Time, TimelineKind.Decision, enemyId, otherId, note: note));

        private bool Ready(Brain brain, EnemyMove move) => !brain.ReadyAt.TryGetValue(move, out float at) || Time >= at;

        // The longest range of the moves it can choose (weight above 0); 0 when it has none
        private static float Reach(Brain brain)
        {
            float reach = 0f;
            foreach (var move in brain.Behaviour.Moves)
            {
                if (move.Weight > 0f)
                    reach = Math.Max(reach, move.MaxRange);
            }
            return reach;
        }

        private static float Closest(Brain brain)
        {
            float closest = float.MaxValue;
            foreach (var move in brain.Behaviour.Moves)
            {
                if (move.Weight > 0f)
                    closest = Math.Min(closest, move.MinRange);
            }
            return closest;
        }

        // When the first of its usable moves is off cooldown
        private float NextReady(Brain brain)
        {
            float next = float.MaxValue;
            foreach (var move in brain.Behaviour.Moves)
            {
                if (move.Weight > 0f)
                    next = Math.Min(next, brain.ReadyAt.TryGetValue(move, out float at) ? at : Time);
            }
            return next == float.MaxValue ? Time : next;
        }

        private bool TargetDistance(Brain brain, CombatVector position, out float distance)
        {
            distance = 0f;
            if (brain.Target < 0 || !_world.TryGetPose(brain.Target, out var where, out _))
                return false;
            distance = (where - position).Flat.Length;
            return true;
        }

        private CombatVector ToTarget(Brain brain, CombatVector position) =>
            brain.Target >= 0 && _world.TryGetPose(brain.Target, out var where, out _) ? Unit((where - position).Flat) : CombatVector.Zero;

        private static CombatVector Away(CombatVector face) => new CombatVector(-face.X, 0f, -face.Z);

        private static CombatVector Unit(CombatVector vector)
        {
            float length = vector.Length;
            return length < 1e-5f ? CombatVector.Zero : new CombatVector(vector.X / length, vector.Y / length, vector.Z / length);
        }

        private Brain Find(int enemyId) => _brains.TryGetValue(enemyId, out var brain) ? brain : null;

        private sealed class Brain
        {
            public Brain(int id, EnemyBehaviour behaviour, CombatVector home)
            {
                Id = id;
                Behaviour = behaviour;
                Home = home;
                Position = home;
            }

            public int Id { get; }
            public EnemyBehaviour Behaviour { get; }
            public CombatVector Home { get; }
            public CombatVector Position;
            public EnemyState State;
            public EnemyIntent Intent;
            public int Target = -1;
            public float LastSeenAt;
            public float NoticeUntil;
            public float RecoverUntil;
            public float BackOffUntil;
            public float BackOffRolledAt = float.NegativeInfinity;
            public float BlockUntil;
            public bool Blocking;
            public float WantSince = -1f;
            public float WantAfter;
            public float CircleSign = 1f;
            public float CircleSwitchAt;
            public bool Attacking;
            public EnemyMove Move;
            public int Swing;
            public bool ComboLanded;
            public readonly Dictionary<EnemyMove, float> ReadyAt = new Dictionary<EnemyMove, float>();
            public CombatVector? Goal;
            public int PatrolIndex;
            public float PauseUntil;
        }
    }
}
