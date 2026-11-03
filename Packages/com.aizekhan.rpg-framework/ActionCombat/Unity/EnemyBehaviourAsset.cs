using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// How a kind of monster notices, moves and fights (<see cref="EnemyBehaviour"/>), tuned in the inspector:
    /// Create > RPG Framework > Action Combat > Enemy Behaviour. Its moves are attacks with ranges, commit shares,
    /// holds, weights, cooldowns and combo links; they can come from its weapon (<see cref="WeaponMoveSet"/>), with the
    /// monster's own added.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Action Combat/Enemy Behaviour", fileName = "EnemyBehaviour")]
    public sealed class EnemyBehaviourAsset : ScriptableObject
    {
        /// <summary>
        /// A move is chosen <see cref="Factor"/> times as often against a target doing <see cref="TargetState"/>
        /// (<see cref="EnemyMove.SetWeightAgainst"/>).
        /// </summary>
        [Serializable]
        public sealed class WeightAgainst
        {
            public CombatState TargetState = CombatState.Blocking;

            [Min(0f)] public float Factor = 1f;
        }

        [Serializable]
        public sealed class Move
        {
            public AttackAsset Attack;

            [Tooltip("Used from this distance to the target (centre to centre)")]
            [Min(0f)] public float MinRange;

            [Tooltip("Used up to this distance; a lunge has a long one")]
            [Min(0f)] public float MaxRange = 2f;

            [Min(0f)] public float Weight = 1f;

            [Tooltip("Weight x Factor against a target doing this: 3 against Blocking for a heavy blow, 0: never against it")]
            public List<WeightAgainst> Against = new List<WeightAgainst>();

            [Tooltip("Seconds before it is chosen again")]
            [Min(0f)] public float Cooldown;

            [Tooltip("Index of the combo's next move in this list (played when this one hit or was blocked); -1: none")]
            public int Next = -1;

            [Tooltip("Off: the next move is only a combo link, never chosen on its own")]
            public bool Opener = true;

            [Tooltip("Turns towards the target during the wind-up (never in the hit window)")]
            public bool TrackDuringWindUp = true;

            [Tooltip("Commits to the blow only this deep in its reach (Max Range x this): 0.75 closes to three quarters first; 1: at the edge")]
            [Range(0f, 1f)] public float CommitShare = 1f;

            [Tooltip("Starts anywhere in Max Range and holds the wind-up, closing in, until the target is within the commit range (a wide swing)")]
            public bool HoldUntilInRange;

            [Tooltip("The longest hold, seconds; then it swings")]
            [Min(0f)] public float MaxHold = 1.5f;

            [Tooltip("Closing-in speed while holding, m/s; 0: the Approach Speed")]
            [Min(0f)] public float HoldSpeed;
        }

        [Header("Perception")]
        [SerializeField, Min(0f)] private float _sightRadius = 12f;
        [SerializeField, Range(0f, 360f)] private float _viewAngle = 120f;
        [Tooltip("Notices anyone this close, whichever way it faces")]
        [SerializeField, Min(0f)] private float _hearingRadius = 3f;
        [Tooltip("Keeps chasing this long after it lost sight")]
        [SerializeField, Min(0f)] private float _memorySeconds = 4f;
        [Tooltip("The Notice state (a roar) before the chase; 0: none")]
        [SerializeField, Min(0f)] private float _noticeSeconds = 0.8f;
        [Tooltip("Gives up beyond this distance from home; 0: never")]
        [SerializeField, Min(0f)] private float _leashRadius = 30f;

        [Header("Patrol (waypoints are on the enemy component)")]
        [SerializeField, Min(0f)] private float _wanderRadius;
        [SerializeField, Min(0f)] private float _patrolPauseSeconds = 2f;

        [Header("Distances")]
        [Tooltip("Beyond this it runs; inside it walks")]
        [SerializeField, Min(0f)] private float _engageDistance = 5f;
        [Tooltip("The distance it keeps while circling, waiting for its turn")]
        [SerializeField, Min(0f)] private float _circleDistance = 3f;
        [Tooltip("Closer than this while waiting, it steps back")]
        [SerializeField, Min(0f)] private float _backOffDistance = 1.5f;
        [SerializeField, Min(0f)] private float _personalSpace = 1.1f;

        [Header("Speeds (m/s) and turning (deg/s)")]
        [SerializeField, Min(0f)] private float _patrolSpeed = 1.5f;
        [SerializeField, Min(0f)] private float _chaseSpeed = 4.5f;
        [SerializeField, Min(0f)] private float _approachSpeed = 2f;
        [SerializeField, Min(0f)] private float _circleSpeed = 1.4f;
        [SerializeField, Min(0f)] private float _backOffSpeed = 1.8f;
        [SerializeField, Min(0f)] private float _returnSpeed = 3f;
        [SerializeField, Min(0f)] private float _turnSpeed = 360f;
        [SerializeField, Min(0.1f)] private float _circleSwitchSeconds = 2.5f;

        [Header("Fighting")]
        [Tooltip("Moves of the weapon it fights with (combos in the set); the list below adds the monster's own")]
        [SerializeField] private WeaponMoveSet _weapon;
        [SerializeField] private List<Move> _moves = new List<Move>();
        [Tooltip("The pause after an attack or a combo: the player's punish window")]
        [SerializeField, Min(0f)] private float _recoveryPause = 0.6f;
        [Tooltip("Chance that after an attack it falls back (circling out) to the range of the Reposition Move before its next turn")]
        [SerializeField, Range(0f, 1f)] private float _repositionChance;
        [Tooltip("Index in the list above of the move it falls back for (a rush); -1: the one with the longest Min Range")]
        [SerializeField] private int _repositionMove = -1;
        [SerializeField, Min(0f)] private float _repositionSeconds = 2f;

        [Header("Defence (when the target swings at it)")]
        [SerializeField, Range(0f, 1f)] private float _blockChance;
        [SerializeField, Min(0f)] private float _blockSeconds = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _dodgeChance;
        [Tooltip("Pressed: after this many swings of the target within Pressed Seconds the pressed chances are rolled instead; 0: never")]
        [SerializeField, Range(0, EnemyBehaviour.MaxPressedSwings)] private int _pressedSwings;
        [SerializeField, Min(0f)] private float _pressedSeconds = 3f;
        [SerializeField, Range(0f, 1f)] private float _pressedBlockChance;
        [SerializeField, Range(0f, 1f)] private float _pressedDodgeChance;
        [Tooltip("Below this share of health it may back off")]
        [SerializeField, Range(0f, 1f)] private float _backOffHealthShare;
        [SerializeField, Range(0f, 1f)] private float _backOffChance = 0.3f;
        [SerializeField, Min(0f)] private float _backOffSeconds = 1f;

        [Header("Death")]
        [Tooltip("How many death clips the animator has (DeathIndex 0..n-1)")]
        [SerializeField, Min(1)] private int _deathVariants = 1;

        public IReadOnlyList<Move> Moves => _moves;

        /// <summary>
        /// The weapon whose moves it fights with, or null.
        /// </summary>
        public WeaponMoveSet Weapon
        {
            get => _weapon;
            set => _weapon = value;
        }

        /// <summary>
        /// What is wrong with the moves (a move without an attack, no opener with a weight, a combo link to itself or
        /// out of the list). A move added through <c>SerializedProperty</c> (arraySize++) starts at zero, not at the
        /// field defaults: Weight 0, Next 0 and Commit Share 0 (built as 1). The inspector logs these as warnings.
        /// </summary>
        public List<string> Problems()
        {
            var problems = MoveProblems(_moves, "Move");
            bool anyOpener = HasOpener(_moves) || (_weapon != null && HasOpener(_weapon.Moves));
            if ((_moves.Count > 0 || _weapon != null) && !anyOpener)
                problems.Add("No move can be chosen: every opener has Weight 0 (or no attack)");
            return problems;
        }

        /// <summary>
        /// What is wrong with a list of moves; <paramref name="label"/> names a move in the messages.
        /// </summary>
        public static List<string> MoveProblems(IReadOnlyList<Move> moves, string label)
        {
            var problems = new List<string>();
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (move == null)
                    continue;
                if (move.Attack == null)
                    problems.Add($"{label} {i} has no attack");
                if (move.Next == i)
                    problems.Add($"{label} {i} links its combo to itself (Next = {i}); use -1 for none");
                else if (move.Next >= moves.Count || move.Next < -1)
                    problems.Add($"{label} {i} links its combo to move {move.Next}, which is not in the list");
                if (move.MaxRange < move.MinRange)
                    problems.Add($"{label} {i}: Max Range is below Min Range");
                if (move.Against != null && move.Against.Exists(a => a != null && a.Factor < 0f))
                    problems.Add($"{label} {i}: a weight factor below 0");
                if (move.HoldUntilInRange && move.MaxHold <= 0f)
                    problems.Add($"{label} {i}: holds until in range but Max Hold is 0 (it never holds)");
            }
            return problems;
        }

        private static bool HasOpener(IReadOnlyList<Move> moves)
        {
            foreach (var move in moves)
            {
                if (move != null && move.Opener && move.Weight > 0f && move.Attack != null)
                    return true;
            }
            return false;
        }

        private void OnValidate()
        {
            foreach (var problem in Problems())
                Debug.LogWarning($"[Enemy Behaviour] {name}: {problem}", this);
        }

        /// <summary>
        /// The rules' behaviour (a new one each call: the patrol points are the enemy's own).
        /// </summary>
        public EnemyBehaviour Build(IReadOnlyList<CombatVector> patrolPoints = null)
        {
            var behaviour = new EnemyBehaviour
            {
                SightRadius = _sightRadius,
                ViewAngle = _viewAngle,
                HearingRadius = _hearingRadius,
                MemorySeconds = _memorySeconds,
                NoticeSeconds = _noticeSeconds,
                LeashRadius = _leashRadius,
                PatrolPoints = patrolPoints ?? Array.Empty<CombatVector>(),
                WanderRadius = _wanderRadius,
                PatrolPauseSeconds = _patrolPauseSeconds,
                EngageDistance = _engageDistance,
                CircleDistance = _circleDistance,
                BackOffDistance = _backOffDistance,
                PersonalSpace = _personalSpace,
                PatrolSpeed = _patrolSpeed,
                ChaseSpeed = _chaseSpeed,
                ApproachSpeed = _approachSpeed,
                CircleSpeed = _circleSpeed,
                BackOffSpeed = _backOffSpeed,
                ReturnSpeed = _returnSpeed,
                TurnSpeed = _turnSpeed,
                CircleSwitchSeconds = _circleSwitchSeconds,
                RecoveryPause = _recoveryPause,
                RepositionChance = _repositionChance,
                RepositionSeconds = _repositionSeconds,
                BlockChance = _blockChance,
                BlockSeconds = _blockSeconds,
                DodgeChance = _dodgeChance,
                PressedSwings = Mathf.Clamp(_pressedSwings, 0, EnemyBehaviour.MaxPressedSwings),
                PressedSeconds = _pressedSeconds,
                PressedBlockChance = _pressedBlockChance,
                PressedDodgeChance = _pressedDodgeChance,
                BackOffHealthShare = _backOffHealthShare,
                BackOffChance = _backOffChance,
                BackOffSeconds = _backOffSeconds,
                DeathVariants = _deathVariants
            };

            if (_weapon != null)
                BuildMoves(_weapon.Moves, behaviour.Moves);
            var own = BuildMoves(_moves, behaviour.Moves);
            if (_repositionMove >= 0 && _repositionMove < own.Length)
                behaviour.RepositionMove = own[_repositionMove];
            return behaviour;
        }

        /// <summary>
        /// Builds a list of moves (combo links by index within it) and adds the openers to <paramref name="into"/>.
        /// </summary>
        /// <returns>Every move built, by its index in <paramref name="moves"/> (null for one without an attack).</returns>
        public static EnemyMove[] BuildMoves(IReadOnlyList<Move> moves, IList<EnemyMove> into)
        {
            var built = new EnemyMove[moves.Count];
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (move?.Attack == null)
                    continue;
                built[i] = new EnemyMove(move.Attack.Definition, move.MinRange, Mathf.Max(move.MinRange, move.MaxRange))
                {
                    Weight = move.Weight,
                    Cooldown = move.Cooldown,
                    TrackDuringWindUp = move.TrackDuringWindUp,
                    CommitShare = move.CommitShare > 0f ? Mathf.Min(1f, move.CommitShare) : 1f,
                    HoldUntilInRange = move.HoldUntilInRange,
                    MaxHold = Mathf.Max(0f, move.MaxHold),
                    HoldSpeed = Mathf.Max(0f, move.HoldSpeed)
                };
                if (move.Against != null)
                {
                    foreach (var against in move.Against)
                    {
                        if (against != null)
                            built[i].SetWeightAgainst(against.TargetState, Mathf.Max(0f, against.Factor));
                    }
                }
            }
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (built[i] == null)
                    continue;
                if (move.Next >= 0 && move.Next < built.Length && move.Next != i)
                    built[i].Next = built[move.Next];
                if (move.Opener)
                    into.Add(built[i]);
            }
            return built;
        }
    }
}
