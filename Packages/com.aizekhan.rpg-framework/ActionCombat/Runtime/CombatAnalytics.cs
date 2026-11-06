using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// What a swing came to (<see cref="SwingOutcome"/>): the best contact it made, or none.
    /// </summary>
    public enum SwingResult
    {
        /// <summary>
        /// It touched nobody.
        /// </summary>
        Miss = 0,

        Hit = 1,
        Blocked = 2,
        Parried = 3,
        Dodged = 4
    }

    /// <summary>
    /// One finished swing, for balancing and telemetry (<see cref="CombatAnalytics.SwingFinished"/>).
    /// </summary>
    public readonly struct SwingOutcome
    {
        public SwingOutcome(int attackerId, int targetId, string attackId, SwingResult result, bool interrupted, float damage,
            float distance, bool combo, float time)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            AttackId = attackId;
            Result = result;
            Interrupted = interrupted;
            Damage = damage;
            Distance = distance;
            Combo = combo;
            Time = time;
        }

        public int AttackerId { get; }

        /// <summary>
        /// Whom the best contact was with, or the target an enemy chose it against; -1 when unknown.
        /// </summary>
        public int TargetId { get; }

        public string AttackId { get; }
        public SwingResult Result { get; }

        /// <summary>
        /// Cut short (a stagger, a dodge out of it).
        /// </summary>
        public bool Interrupted { get; }

        /// <summary>
        /// Damage dealt by all its contacts.
        /// </summary>
        public float Damage { get; }

        /// <summary>
        /// The distance an enemy chose it from (<see cref="AttackChosenEvent.Distance"/>); -1 for a swing no enemy chose.
        /// </summary>
        public float Distance { get; }

        /// <summary>
        /// An enemy played it as a combo's next link.
        /// </summary>
        public bool Combo { get; }

        public float Time { get; }
    }

    /// <summary>
    /// The numbers of one attack (or of all, <see cref="CombatAnalytics.Total"/>).
    /// </summary>
    public sealed class AttackStats
    {
        internal AttackStats(string attackId)
        {
            AttackId = attackId;
        }

        /// <summary>
        /// The attack's id; null for the total.
        /// </summary>
        public string AttackId { get; }

        public int Uses { get; internal set; }
        public int Hits { get; internal set; }
        public int Blocked { get; internal set; }
        public int Parried { get; internal set; }
        public int Dodged { get; internal set; }
        public int Misses { get; internal set; }
        public int Interrupted { get; internal set; }
        public float Damage { get; internal set; }

        internal float DistanceSum;
        internal int DistanceCount;

        /// <summary>
        /// Hits per use (0..1).
        /// </summary>
        public float HitRate => Uses > 0 ? (float)Hits / Uses : 0f;

        /// <summary>
        /// Swings that touched nobody, per use (0..1).
        /// </summary>
        public float MissRate => Uses > 0 ? (float)Misses / Uses : 0f;

        /// <summary>
        /// Swings cut short, per use (0..1).
        /// </summary>
        public float InterruptRate => Uses > 0 ? (float)Interrupted / Uses : 0f;

        /// <summary>
        /// The mean distance enemies chose it from; 0 when no enemy did.
        /// </summary>
        public float AverageDistance => DistanceCount > 0 ? DistanceSum / DistanceCount : 0f;

        internal void Add(SwingOutcome swing)
        {
            Uses++;
            switch (swing.Result)
            {
                case SwingResult.Hit: Hits++; break;
                case SwingResult.Blocked: Blocked++; break;
                case SwingResult.Parried: Parried++; break;
                case SwingResult.Dodged: Dodged++; break;
                default: Misses++; break;
            }
            if (swing.Interrupted)
                Interrupted++;
            Damage += swing.Damage;
            if (swing.Distance >= 0f)
            {
                DistanceSum += swing.Distance;
                DistanceCount++;
            }
        }
    }

    /// <summary>
    /// The numbers of one chain an enemy played: its opener and the combo links that followed (<see cref="Chain"/> as
    /// "slash > slash > overhead").
    /// </summary>
    public sealed class ComboStats
    {
        internal ComboStats(string chain)
        {
            Chain = chain;
        }

        public string Chain { get; }

        /// <summary>
        /// Times it was played to this length.
        /// </summary>
        public int Uses { get; internal set; }

        /// <summary>
        /// Its swings, all plays together.
        /// </summary>
        public int Swings { get; internal set; }

        public int Hits { get; internal set; }
        public float Damage { get; internal set; }

        /// <summary>
        /// Hits per swing (0..1).
        /// </summary>
        public float HitRate => Swings > 0 ? (float)Hits / Swings : 0f;
    }

    /// <summary>
    /// Combat analytics for balancing: listens to a fight's bus and counts, per attack and in total, uses, hits,
    /// blocks, parries, dodges, misses, interruptions, damage and the distance enemies chose each attack from; per
    /// enemy chain (an opener and its combo links) how well it lands (<see cref="Best"/>, <see cref="Worst"/>); and the
    /// approaches enemies made with an attack token that ended with no attack (<see cref="FailedApproaches"/>). Each
    /// finished swing is also raised as <see cref="SwingFinished"/>, for the game's telemetry. Only the attackers
    /// <see cref="Counts"/> accepts are counted (null: everyone).
    /// </summary>
    public sealed class CombatAnalytics : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly Dictionary<long, Swing> _swings = new Dictionary<long, Swing>();
        private readonly Dictionary<int, long> _currentSwing = new Dictionary<int, long>();
        private readonly Dictionary<string, AttackStats> _attacks = new Dictionary<string, AttackStats>();
        private readonly List<AttackStats> _attackList = new List<AttackStats>();
        private readonly Dictionary<string, ComboStats> _combos = new Dictionary<string, ComboStats>();
        private readonly List<ComboStats> _comboList = new List<ComboStats>();
        private readonly Dictionary<int, Chain> _chains = new Dictionary<int, Chain>();
        private readonly HashSet<int> _approaching = new HashSet<int>();

        public CombatAnalytics(IEventBus events, Func<int, bool> counts = null)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            Counts = counts;
            _subscriptions.Add(events.Subscribe<AttackStartedEvent>(OnStarted));
            _subscriptions.Add(events.Subscribe<AttackChosenEvent>(OnChosen));
            _subscriptions.Add(events.Subscribe<ContactResolvedEvent>(OnContact));
            _subscriptions.Add(events.Subscribe<AttackEndedEvent>(OnEnded));
            _subscriptions.Add(events.Subscribe<AttackTokenGrantedEvent>(OnGranted));
            _subscriptions.Add(events.Subscribe<AttackTokenReleasedEvent>(OnReleased));
            _subscriptions.Add(events.Subscribe<EnemyDiedEvent>(e => EndChain(e.EnemyId)));
        }

        /// <summary>
        /// Which attackers are counted (the player's side only, the enemies only); null: everyone.
        /// </summary>
        public Func<int, bool> Counts { get; set; }

        /// <summary>
        /// A swing is over (after its attack ended), with what it came to.
        /// </summary>
        public event Action<SwingOutcome> SwingFinished;

        public AttackStats Total { get; private set; } = new AttackStats(null);

        /// <summary>
        /// Every attack used, in the order first seen.
        /// </summary>
        public IReadOnlyList<AttackStats> Attacks => _attackList;

        /// <summary>
        /// Every enemy chain played, in the order first finished.
        /// </summary>
        public IReadOnlyList<ComboStats> Combos => _comboList;

        /// <summary>
        /// Attack tokens enemies were granted.
        /// </summary>
        public int Approaches { get; private set; }

        /// <summary>
        /// Tokens given up without an attack (the target got away, no move was ready).
        /// </summary>
        public int FailedApproaches { get; private set; }

        public float FailedApproachRate => Approaches > 0 ? (float)FailedApproaches / Approaches : 0f;

        /// <returns>The attack's numbers, or null when it was never used.</returns>
        public AttackStats Of(string attackId) =>
            attackId != null && _attacks.TryGetValue(attackId, out var stats) ? stats : null;

        /// <summary>
        /// The chains played at least <paramref name="minUses"/> times, the best landing first.
        /// </summary>
        public List<ComboStats> Best(int minUses = 3) => Ranked(minUses, best: true);

        /// <summary>
        /// The chains played at least <paramref name="minUses"/> times, the worst landing first.
        /// </summary>
        public List<ComboStats> Worst(int minUses = 3) => Ranked(minUses, best: false);

        /// <summary>
        /// Forgets every number (a new playtest); swings under way are still finished.
        /// </summary>
        public void Reset()
        {
            Total = new AttackStats(null);
            _attacks.Clear();
            _attackList.Clear();
            _combos.Clear();
            _comboList.Clear();
            _chains.Clear();
            _approaching.Clear();
            Approaches = 0;
            FailedApproaches = 0;
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
        }

        private bool Counted(int attackerId) => Counts == null || Counts(attackerId);

        private static long Key(int entity, int swing) => ((long)entity << 32) | (uint)swing;

        private void OnStarted(AttackStartedEvent started)
        {
            if (!Counted(started.EntityId))
                return;
            long key = Key(started.EntityId, started.SwingId);
            _swings[key] = new Swing { Attacker = started.EntityId, AttackId = started.AttackId, Target = -1, Distance = -1f };
            _currentSwing[started.EntityId] = key;
        }

        // The director publishes its choice right after the swing started
        private void OnChosen(AttackChosenEvent chosen)
        {
            _approaching.Remove(chosen.EnemyId);
            if (!Counted(chosen.EnemyId))
                return;
            if (_currentSwing.TryGetValue(chosen.EnemyId, out long key) && _swings.TryGetValue(key, out var swing))
            {
                swing.Distance = chosen.Distance;
                swing.Combo = chosen.Combo;
                if (swing.Target < 0)
                    swing.Target = chosen.TargetId;
            }
            if (!chosen.Combo)
                EndChain(chosen.EnemyId);
            if (!_chains.TryGetValue(chosen.EnemyId, out var chain))
            {
                chain = new Chain();
                _chains.Add(chosen.EnemyId, chain);
            }
            chain.Name = chain.Name == null ? chosen.AttackId : chain.Name + " > " + chosen.AttackId;
            chain.Swings++;
        }

        private void OnContact(ContactResolvedEvent contact)
        {
            if (contact.Result == ContactResult.Ignored || !_swings.TryGetValue(Key(contact.AttackerId, contact.SwingId), out var swing))
                return;
            var result = Of(contact.Result);
            if (swing.Result == SwingResult.Miss || Rank(result) < Rank(swing.Result))
            {
                swing.Result = result;
                swing.Target = contact.TargetId;
            }
            swing.Damage += contact.Damage;
        }

        private void OnEnded(AttackEndedEvent ended)
        {
            long key = Key(ended.EntityId, ended.SwingId);
            if (!_swings.TryGetValue(key, out var swing))
                return;
            _swings.Remove(key);
            if (_currentSwing.TryGetValue(ended.EntityId, out long current) && current == key)
                _currentSwing.Remove(ended.EntityId);

            var outcome = new SwingOutcome(swing.Attacker, swing.Target, swing.AttackId, swing.Result, ended.Interrupted, swing.Damage,
                swing.Distance, swing.Combo, ended.Time);
            Total.Add(outcome);
            string id = swing.AttackId ?? "";
            if (!_attacks.TryGetValue(id, out var stats))
            {
                stats = new AttackStats(id);
                _attacks.Add(id, stats);
                _attackList.Add(stats);
            }
            stats.Add(outcome);
            if (swing.Distance >= 0f && _chains.TryGetValue(swing.Attacker, out var chain))
            {
                if (outcome.Result == SwingResult.Hit)
                    chain.Hits++;
                chain.Damage += outcome.Damage;
            }
            SwingFinished?.Invoke(outcome);
        }

        private void OnGranted(AttackTokenGrantedEvent granted)
        {
            if (!Counted(granted.EnemyId))
                return;
            Approaches++;
            _approaching.Add(granted.EnemyId);
        }

        private void OnReleased(AttackTokenReleasedEvent released)
        {
            if (_approaching.Remove(released.EnemyId))
                FailedApproaches++;
            // The chain ends when the enemy gives up its turn (the director releases it after the last link)
            EndChain(released.EnemyId);
        }

        private void EndChain(int enemyId)
        {
            if (!_chains.TryGetValue(enemyId, out var chain) || chain.Name == null)
                return;
            if (!_combos.TryGetValue(chain.Name, out var stats))
            {
                stats = new ComboStats(chain.Name);
                _combos.Add(chain.Name, stats);
                _comboList.Add(stats);
            }
            stats.Uses++;
            stats.Swings += chain.Swings;
            stats.Hits += chain.Hits;
            stats.Damage += chain.Damage;
            chain.Name = null;
            chain.Swings = 0;
            chain.Hits = 0;
            chain.Damage = 0f;
        }

        private List<ComboStats> Ranked(int minUses, bool best)
        {
            var ranked = new List<ComboStats>();
            foreach (var combo in _comboList)
            {
                if (combo.Uses >= minUses)
                    ranked.Add(combo);
            }
            ranked.Sort((a, b) =>
            {
                int order = best ? b.HitRate.CompareTo(a.HitRate) : a.HitRate.CompareTo(b.HitRate);
                return order != 0 ? order : string.CompareOrdinal(a.Chain, b.Chain);
            });
            return ranked;
        }

        private static SwingResult Of(ContactResult result)
        {
            switch (result)
            {
                case ContactResult.Hit: return SwingResult.Hit;
                case ContactResult.Blocked: return SwingResult.Blocked;
                case ContactResult.Parried: return SwingResult.Parried;
                case ContactResult.Dodged: return SwingResult.Dodged;
                default: return SwingResult.Miss;
            }
        }

        // The better contact wins: a hit over a block over a parry over a dodge
        private static int Rank(SwingResult result) => (int)result;

        private sealed class Swing
        {
            public int Attacker;
            public string AttackId;
            public int Target;
            public SwingResult Result;
            public float Damage;
            public float Distance;
            public bool Combo;
        }

        private sealed class Chain
        {
            public string Name;
            public int Swings;
            public int Hits;
            public float Damage;
        }
    }
}
