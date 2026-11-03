using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// What an enemy of action combat is doing (<see cref="EnemyDirector"/>).
    /// </summary>
    public enum EnemyState
    {
        /// <summary>
        /// Standing at home, nobody noticed.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Walking its waypoints or wandering around home.
        /// </summary>
        Patrol = 1,

        /// <summary>
        /// Just noticed a target (a roar, a taunt) before the chase.
        /// </summary>
        Notice = 2,

        /// <summary>
        /// Running to the target from afar.
        /// </summary>
        Chase = 3,

        /// <summary>
        /// Walking in to strike (it holds an attack token).
        /// </summary>
        Approach = 4,

        /// <summary>
        /// Circling the target at fighting distance, waiting for a token.
        /// </summary>
        Circle = 5,

        /// <summary>
        /// Stepping back (too close while waiting, or hurt).
        /// </summary>
        BackOff = 6,

        /// <summary>
        /// Making an attack (its wind-up, window and recovery).
        /// </summary>
        Attacking = 7,

        /// <summary>
        /// The pause after an attack or a combo: the player's window to punish.
        /// </summary>
        Recovering = 8,

        /// <summary>
        /// Blocking or dodging the target's blow.
        /// </summary>
        Defending = 9,

        /// <summary>
        /// Staggered or knocked down by a hit.
        /// </summary>
        Reacting = 10,

        /// <summary>
        /// Lost the target; going home.
        /// </summary>
        Returning = 11,

        Dead = 12
    }

    /// <summary>
    /// One move in an enemy's set: an attack, the distances it is used from, how close it commits to it, how often
    /// (weight), how soon again (cooldown) and what may follow it in a combo (only when this one hit or was blocked).
    /// A quick blow starts only well inside its reach (<see cref="CommitShare"/>); a wide swing may start at the edge
    /// and hold its wind-up while it closes in (<see cref="HoldUntilInRange"/>).
    /// </summary>
    public sealed class EnemyMove
    {
        private float _commitShare = 1f;
        private float _maxHold = 1.5f;
        private float _holdSpeed;
        private List<(CombatState State, float Factor)> _against;

        public EnemyMove(AttackDefinition attack, float minRange, float maxRange)
        {
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));
            if (maxRange < minRange)
                throw new ArgumentException("The longest range is below the shortest", nameof(maxRange));
            MinRange = minRange;
            MaxRange = maxRange;
        }

        public AttackDefinition Attack { get; }

        /// <summary>
        /// Used from this distance to the target (centre to centre) and further.
        /// </summary>
        public float MinRange { get; }

        /// <summary>
        /// Used up to this distance; a gap closer (a lunge) has a long one.
        /// </summary>
        public float MaxRange { get; }

        public float Weight { get; set; } = 1f;

        /// <summary>
        /// It is chosen <paramref name="factor"/> times as often against a target in <paramref name="targetState"/>: 3
        /// against <see cref="CombatState.Blocking"/> for a heavy blow, 0 never against it. Set again to change it; 1 removes it. The director reads the target's state from the
        /// world when it is <see cref="IEnemyTargetStates"/>, otherwise from the rules; a target whose state it cannot
        /// tell gets the plain <see cref="Weight"/>.
        /// </summary>
        public void SetWeightAgainst(CombatState targetState, float factor)
        {
            NotNegative(factor, nameof(factor));
            if (_against == null)
                _against = new List<(CombatState, float)>();
            for (int i = 0; i < _against.Count; i++)
            {
                if (_against[i].State != targetState)
                    continue;
                if (factor == 1f)
                    _against.RemoveAt(i);
                else
                    _against[i] = (targetState, factor);
                return;
            }
            if (factor != 1f)
                _against.Add((targetState, factor));
        }

        /// <summary>
        /// How much more often it is chosen against a target in <paramref name="targetState"/> (1 = as often).
        /// </summary>
        public float FactorAgainst(CombatState targetState)
        {
            if (_against == null)
                return 1f;
            for (int i = 0; i < _against.Count; i++)
            {
                if (_against[i].State == targetState)
                    return _against[i].Factor;
            }
            return 1f;
        }

        /// <summary>
        /// Seconds before this move is chosen again.
        /// </summary>
        public float Cooldown { get; set; }

        /// <summary>
        /// The combo's next link, played right after this one when it hit or was blocked and the target is still in its
        /// range; null ends the combo.
        /// </summary>
        public EnemyMove Next { get; set; }

        /// <summary>
        /// It may turn towards the target during the wind-up (never in the active window or the recovery).
        /// </summary>
        public bool TrackDuringWindUp { get; set; } = true;

        /// <summary>
        /// It commits to the blow only this deep in its reach: within <see cref="CommitRange"/> =
        /// <see cref="MaxRange"/> x this (no nearer than <see cref="MinRange"/>). 0.75 makes a dagger close to three
        /// quarters of its reach first, so a target that keeps moving is still in reach when the blow lands. 1 = at
        /// the edge.
        /// </summary>
        public float CommitShare
        {
            get => _commitShare;
            set
            {
                if (float.IsNaN(value) || value <= 0f || value > 1f)
                    throw new ArgumentOutOfRangeException(nameof(CommitShare), value, "A share of the reach, above 0 up to 1");
                _commitShare = value;
            }
        }

        /// <summary>
        /// The distance it commits to the blow from.
        /// </summary>
        public float CommitRange => Math.Max(MinRange, MaxRange * _commitShare);

        /// <summary>
        /// It may start anywhere in <see cref="MaxRange"/> and holds the wind-up (<see cref="CombatRules.HoldWindUp"/>,
        /// at the attack's <see cref="AttackDefinition.HoldPoint"/>), tracking and closing in, until the target is
        /// within <see cref="CommitRange"/> or <see cref="MaxHold"/> runs out; then the blow goes on. For a wide swing.
        /// </summary>
        public bool HoldUntilInRange { get; set; }

        /// <summary>
        /// The longest hold, in seconds; then it swings wherever the target is.
        /// </summary>
        public float MaxHold
        {
            get => _maxHold;
            set => _maxHold = NotNegative(value, nameof(MaxHold));
        }

        /// <summary>
        /// Metres per second it closes in while holding; 0 = the behaviour's <see cref="EnemyBehaviour.ApproachSpeed"/>.
        /// </summary>
        public float HoldSpeed
        {
            get => _holdSpeed;
            set => _holdSpeed = NotNegative(value, nameof(HoldSpeed));
        }

        /// <summary>
        /// The longest distance it starts from: <see cref="MaxRange"/> for a held move, <see cref="CommitRange"/>
        /// otherwise.
        /// </summary>
        public float StartRange => HoldUntilInRange ? MaxRange : CommitRange;

        public bool InRange(float distance) => distance >= MinRange && distance <= MaxRange;

        /// <summary>
        /// It may start from this distance (<see cref="MinRange"/> to <see cref="StartRange"/>).
        /// </summary>
        public bool CanStart(float distance) => distance >= MinRange && distance <= StartRange;

        private static float NotNegative(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new ArgumentOutOfRangeException(name, value, "A finite number, 0 or more");
            return value;
        }
    }

    /// <summary>
    /// How an enemy notices, moves and fights; designers tune one per kind of monster (<c>EnemyBehaviourAsset</c>).
    /// </summary>
    public sealed class EnemyBehaviour
    {
        // Perception
        /// <summary>
        /// It sees this far inside its view cone (with a line of sight).
        /// </summary>
        public float SightRadius { get; set; } = 12f;

        /// <summary>
        /// The whole view cone in degrees.
        /// </summary>
        public float ViewAngle { get; set; } = 120f;

        /// <summary>
        /// It notices anyone this close, whichever way it faces and through walls.
        /// </summary>
        public float HearingRadius { get; set; } = 3f;

        /// <summary>
        /// It keeps chasing this long after it lost sight of the target.
        /// </summary>
        public float MemorySeconds { get; set; } = 4f;

        /// <summary>
        /// The Notice state (a roar) before the chase; 0 = none.
        /// </summary>
        public float NoticeSeconds { get; set; } = 0.8f;

        /// <summary>
        /// It gives up when the chase takes it this far from home and walks back, noticing nobody until it is home (a
        /// blow on the way is ignored while it is still beyond this radius); 0 = never.
        /// </summary>
        public float LeashRadius { get; set; } = 30f;

        // Patrol
        /// <summary>
        /// Waypoints to walk in a loop; empty = wander around home within <see cref="WanderRadius"/>.
        /// </summary>
        public IReadOnlyList<CombatVector> PatrolPoints { get; set; } = Array.Empty<CombatVector>();

        /// <summary>
        /// How far from home it wanders without waypoints; 0 = it stands at home.
        /// </summary>
        public float WanderRadius { get; set; }

        /// <summary>
        /// Seconds it waits at each waypoint or wander point.
        /// </summary>
        public float PatrolPauseSeconds { get; set; } = 2f;

        // Distances
        /// <summary>
        /// Beyond this it runs (Chase); inside it walks (Approach, Circle).
        /// </summary>
        public float EngageDistance { get; set; } = 5f;

        /// <summary>
        /// The distance it keeps while circling and waiting for its turn.
        /// </summary>
        public float CircleDistance { get; set; } = 3f;

        /// <summary>
        /// Closer than this while waiting, it steps back.
        /// </summary>
        public float BackOffDistance { get; set; } = 1.5f;

        /// <summary>
        /// It keeps this far from other enemies (they push apart).
        /// </summary>
        public float PersonalSpace { get; set; } = 1.1f;

        /// <summary>
        /// It closes no nearer to its target than this (centre to centre): a move's approach motion and a held
        /// wind-up's closing in stop here.
        /// </summary>
        public float ContactDistance { get; set; } = 0.6f;

        // Speeds
        public float PatrolSpeed { get; set; } = 1.5f;
        public float ChaseSpeed { get; set; } = 4.5f;
        public float ApproachSpeed { get; set; } = 2f;
        public float CircleSpeed { get; set; } = 1.4f;
        public float BackOffSpeed { get; set; } = 1.8f;
        public float ReturnSpeed { get; set; } = 3f;

        /// <summary>
        /// Degrees per second it turns.
        /// </summary>
        public float TurnSpeed { get; set; } = 360f;

        /// <summary>
        /// Seconds before it changes the way it circles.
        /// </summary>
        public float CircleSwitchSeconds { get; set; } = 2.5f;

        // Fighting
        public IList<EnemyMove> Moves { get; } = new List<EnemyMove>();

        /// <summary>
        /// The pause after an attack or a combo: the player's punish window.
        /// </summary>
        public float RecoveryPause { get; set; } = 0.6f;

        /// <summary>
        /// Chance (0..1), rolled when an attack or a combo ends, that it falls back before its next turn: it gives up its
        /// token and circles out until the target is in <see cref="RepositionMove"/>'s range (or
        /// <see cref="RepositionSeconds"/> run out), then asks again and opens with that move when it is ready. For a
        /// monster that rushes in again rather than staying at arm's length. 0 = never.
        /// </summary>
        public float RepositionChance { get; set; }

        /// <summary>
        /// The move it falls back for (one of <see cref="Moves"/>, such as a rush); null = the move with the longest
        /// <see cref="EnemyMove.MinRange"/>.
        /// </summary>
        public EnemyMove RepositionMove { get; set; }

        /// <summary>
        /// The longest fall back, in seconds.
        /// </summary>
        public float RepositionSeconds { get; set; } = 2f;

        // Defence (rolled when the target starts a swing at it)
        /// <summary>
        /// The most swings <see cref="PressedSwings"/> may count.
        /// </summary>
        public const int MaxPressedSwings = 16;

        private int _pressedSwings;

        public float BlockChance { get; set; }

        /// <summary>
        /// Seconds it holds the block it raised.
        /// </summary>
        public float BlockSeconds { get; set; } = 0.8f;

        public float DodgeChance { get; set; }

        /// <summary>
        /// Pressed: when the target has started this many swings (<see cref="AttackStartedEvent"/>, counting the one
        /// now) within <see cref="PressedSeconds"/>, the director rolls <see cref="PressedBlockChance"/> and
        /// <see cref="PressedDodgeChance"/> instead of <see cref="BlockChance"/> and <see cref="DodgeChance"/>. 0 = never
        /// (up to <see cref="MaxPressedSwings"/>).
        /// </summary>
        public int PressedSwings
        {
            get => _pressedSwings;
            set
            {
                if (value < 0 || value > MaxPressedSwings)
                    throw new ArgumentOutOfRangeException(nameof(PressedSwings), value, $"0 to {MaxPressedSwings}");
                _pressedSwings = value;
            }
        }

        public float PressedSeconds { get; set; } = 3f;

        public float PressedBlockChance { get; set; }

        public float PressedDodgeChance { get; set; }

        /// <summary>
        /// Below this share of health (0..1) it may back off (<see cref="BackOffChance"/>, rolled each time it would
        /// close in).
        /// </summary>
        public float BackOffHealthShare { get; set; }

        public float BackOffChance { get; set; } = 0.3f;

        /// <summary>
        /// Seconds a back-off lasts.
        /// </summary>
        public float BackOffSeconds { get; set; } = 1f;

        // Death
        /// <summary>
        /// How many death clips it has; <see cref="EnemyDiedEvent.Variant"/> picks one (seeded).
        /// </summary>
        public int DeathVariants { get; set; } = 1;
    }

    /// <summary>
    /// Attack slots: at most <see cref="MaxPerTarget"/> enemies attack one target at a time; the rest circle and wait.
    /// Each round the free slots go to the waiting enemies with the highest priority.
    /// </summary>
    public sealed class AttackTokens
    {
        private readonly Dictionary<int, int> _holders = new Dictionary<int, int>();
        private readonly Dictionary<int, List<(int Enemy, float Priority)>> _wanted = new Dictionary<int, List<(int, float)>>();
        private readonly List<int> _targets = new List<int>();

        public AttackTokens(int maxPerTarget = 2)
        {
            MaxPerTarget = maxPerTarget;
        }

        /// <summary>
        /// Enemies that may attack one target at once (0 or less: no limit).
        /// </summary>
        public int MaxPerTarget { get; set; }

        public bool Holds(int enemyId) => _holders.ContainsKey(enemyId);

        /// <summary>
        /// The target the enemy holds a token for, or -1.
        /// </summary>
        public int TargetOf(int enemyId) => _holders.TryGetValue(enemyId, out int target) ? target : -1;

        public int HoldersOf(int targetId)
        {
            int count = 0;
            foreach (var held in _holders.Values)
            {
                if (held == targetId)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Asks for a token this round (higher priority first); <see cref="Grant"/> hands them out.
        /// </summary>
        public void Want(int enemyId, int targetId, float priority)
        {
            if (Holds(enemyId))
                return;
            if (!_wanted.TryGetValue(targetId, out var list))
            {
                list = new List<(int, float)>();
                _wanted.Add(targetId, list);
            }
            list.Add((enemyId, priority));
        }

        /// <summary>
        /// Hands out the free slots of this round's requests.
        /// </summary>
        /// <returns>The enemies that got one.</returns>
        public List<(int Enemy, int Target)> Grant()
        {
            var granted = new List<(int Enemy, int Target)>();
            Grant(granted);
            return granted;
        }

        /// <summary>
        /// Hands out the free slots of this round's requests without garbage: <paramref name="granted"/> is cleared and
        /// filled with the enemies that got one.
        /// </summary>
        public void Grant(List<(int Enemy, int Target)> granted)
        {
            if (granted == null)
                throw new ArgumentNullException(nameof(granted));
            granted.Clear();
            _targets.Clear();
            _targets.AddRange(_wanted.Keys);
            foreach (int target in _targets)
            {
                var list = _wanted[target];
                SortByPriority(list);
                int free = MaxPerTarget <= 0 ? int.MaxValue : MaxPerTarget - HoldersOf(target);
                for (int i = 0; i < list.Count && free > 0; i++)
                {
                    if (Holds(list[i].Enemy))
                        continue;
                    _holders.Add(list[i].Enemy, target);
                    granted.Add((list[i].Enemy, target));
                    free--;
                }
                list.Clear();
            }
        }

        /// <returns>True when it held one.</returns>
        public bool Release(int enemyId) => _holders.Remove(enemyId);

        // Highest priority first, then the lower id. An insertion sort in place: List.Sort makes garbage under Unity's
        // Mono even with a cached comparer, and a target has only a handful of enemies waiting
        private static void SortByPriority(List<(int Enemy, float Priority)> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var item = list[i];
                int j = i - 1;
                while (j >= 0 && Before(item, list[j]))
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = item;
            }
        }

        private static bool Before((int Enemy, float Priority) a, (int Enemy, float Priority) b) =>
            a.Priority != b.Priority ? a.Priority > b.Priority : a.Enemy < b.Enemy;
    }

    /// <summary>
    /// What the scene tells the enemies: who they may fight, where everyone is, who can be seen, how hurt they are.
    /// The Unity layer implements it (raycasts for the line of sight); tests fake it.
    /// </summary>
    public interface IEnemyWorld
    {
        /// <summary>
        /// The living characters this enemy may fight (players).
        /// </summary>
        IEnumerable<int> TargetsOf(int enemyId);

        bool TryGetPose(int entityId, out CombatVector position, out CombatVector forward);

        /// <summary>
        /// Nothing blocks the view between the two.
        /// </summary>
        bool CanSee(int fromId, int toId);

        /// <summary>
        /// Health left, 0..1.
        /// </summary>
        float HealthShare(int entityId);

        bool IsAlive(int entityId);
    }

    /// <summary>
    /// A world that also tells the enemies what a fighter is doing, for targets the director's
    /// <see cref="CombatRules"/> do not drive (a runner on a track the game resolves itself). The director asks it
    /// before the rules (<see cref="EnemyMove.SetWeightAgainst"/>).
    /// </summary>
    public interface IEnemyTargetStates
    {
        /// <returns>False when it does not know: the director asks the rules.</returns>
        bool TryGetState(int entityId, out CombatState state);
    }

    /// <summary>
    /// What the body should do this frame: the Unity layer moves and turns it (and animates locomotion from it).
    /// </summary>
    public readonly struct EnemyIntent
    {
        public EnemyIntent(EnemyState state, int targetId, CombatVector move, float speed, CombatVector face, float turnSpeed)
            : this(state, targetId, move, speed, face, turnSpeed, 0f)
        {
        }

        public EnemyIntent(EnemyState state, int targetId, CombatVector move, float speed, CombatVector face, float turnSpeed,
            float height)
        {
            State = state;
            TargetId = targetId;
            Move = move;
            Speed = speed;
            Face = face;
            TurnSpeed = turnSpeed;
            Height = height;
        }

        public EnemyState State { get; }

        /// <summary>
        /// Who it fights, or -1.
        /// </summary>
        public int TargetId { get; }

        /// <summary>
        /// Direction to walk on the ground (unit length, or zero to stand).
        /// </summary>
        public CombatVector Move { get; }

        /// <summary>
        /// Metres per second along <see cref="Move"/>.
        /// </summary>
        public float Speed { get; }

        /// <summary>
        /// Direction to face (zero: keep facing).
        /// </summary>
        public CombatVector Face { get; }

        /// <summary>
        /// Degrees per second it may turn now (0: it may not, as in an attack's active window).
        /// </summary>
        public float TurnSpeed { get; }

        /// <summary>
        /// Metres above the ground on a leap attack's arc (<see cref="AttackMotionKind.Leap"/>); the Unity side raises
        /// the model by it.
        /// </summary>
        public float Height { get; }

        public static EnemyIntent Still(EnemyState state, int target) =>
            new EnemyIntent(state, target, CombatVector.Zero, 0f, CombatVector.Zero, 0f);
    }

    /// <summary>
    /// An enemy noticed a target (<see cref="Struck"/>: because it was hit, without the Notice pause).
    /// </summary>
    public readonly struct EnemyNoticedEvent : IEvent
    {
        public EnemyNoticedEvent(int enemyId, int targetId, bool struck, float time)
        {
            EnemyId = enemyId;
            TargetId = targetId;
            Struck = struck;
            Time = time;
        }

        public int EnemyId { get; }
        public int TargetId { get; }
        public bool Struck { get; }
        public float Time { get; }
    }

    /// <summary>
    /// An enemy changed what it is doing, and why.
    /// </summary>
    public readonly struct EnemyStateChangedEvent : IEvent
    {
        public EnemyStateChangedEvent(int enemyId, EnemyState from, EnemyState to, string reason, float time)
        {
            EnemyId = enemyId;
            From = from;
            To = to;
            Reason = reason;
            Time = time;
        }

        public int EnemyId { get; }
        public EnemyState From { get; }
        public EnemyState To { get; }
        public string Reason { get; }
        public float Time { get; }
    }

    public readonly struct AttackTokenGrantedEvent : IEvent
    {
        public AttackTokenGrantedEvent(int enemyId, int targetId, float time)
        {
            EnemyId = enemyId;
            TargetId = targetId;
            Time = time;
        }

        public int EnemyId { get; }
        public int TargetId { get; }
        public float Time { get; }
    }

    public readonly struct AttackTokenReleasedEvent : IEvent
    {
        public AttackTokenReleasedEvent(int enemyId, int targetId, string reason, float time)
        {
            EnemyId = enemyId;
            TargetId = targetId;
            Reason = reason;
            Time = time;
        }

        public int EnemyId { get; }
        public int TargetId { get; }
        public string Reason { get; }
        public float Time { get; }
    }

    /// <summary>
    /// An enemy chose an attack (<see cref="Combo"/>: as the next link of a combo).
    /// </summary>
    public readonly struct AttackChosenEvent : IEvent
    {
        public AttackChosenEvent(int enemyId, int targetId, string attackId, float distance, bool combo, float time)
        {
            EnemyId = enemyId;
            TargetId = targetId;
            AttackId = attackId;
            Distance = distance;
            Combo = combo;
            Time = time;
        }

        public int EnemyId { get; }
        public int TargetId { get; }
        public string AttackId { get; }
        public float Distance { get; }
        public bool Combo { get; }
        public float Time { get; }
    }

    /// <summary>
    /// An enemy died: play death clip <see cref="Variant"/>; the body stays (the kit's corpse time) before it goes.
    /// </summary>
    public readonly struct EnemyDiedEvent : IEvent
    {
        public EnemyDiedEvent(int enemyId, int variant, float time)
        {
            EnemyId = enemyId;
            Variant = variant;
            Time = time;
        }

        public int EnemyId { get; }
        public int Variant { get; }
        public float Time { get; }
    }
}
