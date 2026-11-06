using System;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// A habit of the target an enemy can learn (<see cref="EnemyAdaptation"/>). Left and right are as the target sees
    /// it, facing the enemy.
    /// </summary>
    [Flags]
    public enum PlayerHabit
    {
        None = 0,

        /// <summary>
        /// Dodges to its left.
        /// </summary>
        DodgesLeft = 1,

        /// <summary>
        /// Dodges to its right.
        /// </summary>
        DodgesRight = 2,

        /// <summary>
        /// Blocks the enemy's blows.
        /// </summary>
        Blocks = 4,

        /// <summary>
        /// Parries the enemy's blows.
        /// </summary>
        Parries = 8,

        /// <summary>
        /// Steps or dodges back out of reach: the enemy's blow found nobody.
        /// </summary>
        Retreats = 16,

        /// <summary>
        /// Swings at the enemy.
        /// </summary>
        Presses = 32,

        All = DodgesLeft | DodgesRight | Blocks | Parries | Retreats | Presses
    }

    /// <summary>
    /// How an enemy learns its target's habits: it remembers the target's last <see cref="Memory"/> actions it saw
    /// (only the habits in <see cref="Notices"/>), and a move with an answer to a habit
    /// (<see cref="EnemyMove.SetAnswer"/>) is chosen more often the more of that memory the habit fills, scaled by
    /// <see cref="Learning"/>. A zombie has none (no adaptation); a mercenary learns fast. The memory is of one target
    /// and is forgotten when the enemy loses it.
    /// </summary>
    public sealed class EnemyAdaptation
    {
        /// <summary>
        /// The most actions <see cref="Memory"/> may hold.
        /// </summary>
        public const int MaxMemory = 16;

        private float _learning = 0.5f;
        private int _memory = 5;

        /// <summary>
        /// How far a habit moves its choices, 0..1: an answer's factor applies in full only when the habit fills the
        /// whole memory and this is 1. 0 = it never learns.
        /// </summary>
        public float Learning
        {
            get => _learning;
            set
            {
                if (float.IsNaN(value) || value < 0f || value > 1f)
                    throw new ArgumentOutOfRangeException(nameof(Learning), value, "0 to 1");
                _learning = value;
            }
        }

        /// <summary>
        /// How many of the target's last actions it remembers (1 to <see cref="MaxMemory"/>); a short memory adapts
        /// and forgets fast.
        /// </summary>
        public int Memory
        {
            get => _memory;
            set
            {
                if (value < 1 || value > MaxMemory)
                    throw new ArgumentOutOfRangeException(nameof(Memory), value, $"1 to {MaxMemory}");
                _memory = value;
            }
        }

        /// <summary>
        /// The habits it pays attention to; the rest are not remembered.
        /// </summary>
        public PlayerHabit Notices { get; set; } = PlayerHabit.All;
    }

    /// <summary>
    /// An enemy saw its target do something it pays attention to (<see cref="EnemyAdaptation.Notices"/>);
    /// <see cref="Share"/> is how much of its memory that habit now fills (0..1).
    /// </summary>
    public readonly struct PlayerHabitNoticedEvent : IEvent
    {
        public PlayerHabitNoticedEvent(int enemyId, int targetId, PlayerHabit habit, float share, float time)
        {
            EnemyId = enemyId;
            TargetId = targetId;
            Habit = habit;
            Share = share;
            Time = time;
        }

        public int EnemyId { get; }
        public int TargetId { get; }
        public PlayerHabit Habit { get; }
        public float Share { get; }
        public float Time { get; }
    }

    // One enemy's memory of its target's actions: a ring of the last Memory habits
    internal sealed class HabitMemory
    {
        private readonly PlayerHabit[] _seen = new PlayerHabit[EnemyAdaptation.MaxMemory];
        private int _next;
        private int _count;

        public int Target = -1;

        public void Clear(int target)
        {
            Target = target;
            _next = 0;
            _count = 0;
        }

        public void Add(PlayerHabit habit, int memory)
        {
            if (_count > memory)
                _count = memory;
            _seen[_next % EnemyAdaptation.MaxMemory] = habit;
            _next = (_next + 1) % EnemyAdaptation.MaxMemory;
            if (_count < memory)
                _count++;
        }

        // The share of the whole memory (not of what is in it yet) the habit fills: a few actions teach little
        public float Share(PlayerHabit habit, int memory)
        {
            int seen = 0;
            int count = Math.Min(_count, memory);
            for (int i = 1; i <= count; i++)
            {
                if ((_seen[(_next - i + EnemyAdaptation.MaxMemory) % EnemyAdaptation.MaxMemory] & habit) != 0)
                    seen++;
            }
            return memory > 0 ? (float)seen / memory : 0f;
        }
    }
}
