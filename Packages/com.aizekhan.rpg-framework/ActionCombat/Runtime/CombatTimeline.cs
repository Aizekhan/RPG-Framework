using System;
using System.Collections.Generic;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// What a timeline entry records.
    /// </summary>
    public enum TimelineKind
    {
        AttackStarted = 0,
        ChargeStarted = 1,
        AttackReleased = 2,
        ActiveStarted = 3,
        ActiveEnded = 4,
        AttackEnded = 5,

        /// <summary>
        /// A contact: <see cref="TimelineEntry.EntityId"/> is the attacker, <see cref="TimelineEntry.OtherId"/> the
        /// target, <see cref="TimelineEntry.Result"/> what it was, <see cref="TimelineEntry.Value"/> the damage.
        /// </summary>
        Contact = 6,

        HitReactStarted = 7,
        HitReactEnded = 8,
        DodgeStarted = 9,
        DodgeEnded = 10,
        BlockRaised = 11,
        BlockLowered = 12,

        /// <summary>
        /// A projectile's swing ended (it hit, hit a wall, or its lifetime ran out).
        /// </summary>
        SwingEnded = 13,

        /// <summary>
        /// An action was refused (<see cref="TimelineEntry.Note"/> says which and in what state): "was the dodge
        /// pressed in time".
        /// </summary>
        Refused = 14,

        /// <summary>
        /// The game's own mark (<see cref="CombatTimeline.Mark"/>): an input, an animation event.
        /// </summary>
        Mark = 15,

        /// <summary>
        /// An enemy's decision (<c>EnemyDirector</c>): a state change, a token, a chosen attack, a defence, and why
        /// (<see cref="TimelineEntry.Note"/>).
        /// </summary>
        Decision = 16
    }

    /// <summary>
    /// One moment of the combat timeline.
    /// </summary>
    public readonly struct TimelineEntry
    {
        public TimelineEntry(float time, TimelineKind kind, int entityId, int otherId = -1, int swingId = 0,
            string attackId = null, ContactResult result = ContactResult.Ignored, float value = 0f, string note = null)
        {
            Time = time;
            Kind = kind;
            EntityId = entityId;
            OtherId = otherId;
            SwingId = swingId;
            AttackId = attackId;
            Result = result;
            Value = value;
            Note = note;
        }

        public float Time { get; }
        public TimelineKind Kind { get; }
        public int EntityId { get; }

        /// <summary>
        /// The other side (a contact's target, a stagger's attacker); -1 when none.
        /// </summary>
        public int OtherId { get; }

        public int SwingId { get; }
        public string AttackId { get; }
        public ContactResult Result { get; }

        /// <summary>
        /// Damage (a contact), seconds (a stagger, a dodge, a charge) or the charge multiplier (a release).
        /// </summary>
        public float Value { get; }

        public string Note { get; }

        /// <summary>
        /// The entry is about this combatant (as the one acting or the one acted on).
        /// </summary>
        public bool Involves(int entityId) => EntityId == entityId || OtherId == entityId;

        public override string ToString() =>
            $"{Time:0.000} {Kind} #{EntityId}{(OtherId >= 0 ? $"->#{OtherId}" : "")}" +
            $"{(SwingId != 0 ? $" swing {SwingId}" : "")}{(AttackId != null ? $" {AttackId}" : "")}" +
            $"{(Kind == TimelineKind.Contact ? $" {Result}" : "")}{(Value != 0f ? $" {Value:0.##}" : "")}" +
            $"{(Note != null ? $" ({Note})" : "")}";
    }

    /// <summary>
    /// The last entries of a fight, timestamped: phases, contacts and their results, damage, dodges, blocks. For a
    /// diagnostics panel ("the monster's strike frame vs the contact vs when damage was taken", "was the dodge
    /// pressed in time"). Keeps at most <see cref="Capacity"/> entries; the oldest go first.
    /// </summary>
    public sealed class CombatTimeline
    {
        private readonly TimelineEntry[] _entries;
        private int _start;

        public CombatTimeline(int capacity = 2048)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A timeline keeps at least one entry");
            _entries = new TimelineEntry[capacity];
        }

        public int Capacity => _entries.Length;
        public int Count { get; private set; }

        /// <summary>
        /// Raised for every entry as it is recorded.
        /// </summary>
        public event Action<TimelineEntry> Recorded;

        /// <summary>
        /// The entry at <paramref name="index"/>, oldest first.
        /// </summary>
        public TimelineEntry this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _entries[(_start + index) % _entries.Length];
            }
        }

        public void Record(TimelineEntry entry)
        {
            if (Count < _entries.Length)
            {
                _entries[(_start + Count) % _entries.Length] = entry;
                Count++;
            }
            else
            {
                _entries[_start] = entry;
                _start = (_start + 1) % _entries.Length;
            }
            Recorded?.Invoke(entry);
        }

        /// <summary>
        /// The game's own mark at a time (an input pressed, an animation event fired).
        /// </summary>
        public void Mark(float time, int entityId, string note) =>
            Record(new TimelineEntry(time, TimelineKind.Mark, entityId, note: note));

        /// <summary>
        /// Every entry, oldest first.
        /// </summary>
        public IEnumerable<TimelineEntry> Entries
        {
            get
            {
                for (int i = 0; i < Count; i++)
                    yield return this[i];
            }
        }

        /// <summary>
        /// The entries about one combatant (acting or acted on), oldest first.
        /// </summary>
        public IEnumerable<TimelineEntry> For(int entityId)
        {
            for (int i = 0; i < Count; i++)
            {
                var entry = this[i];
                if (entry.Involves(entityId))
                    yield return entry;
            }
        }

        /// <summary>
        /// The entries from <paramref name="fromTime"/> on, oldest first.
        /// </summary>
        public IEnumerable<TimelineEntry> Since(float fromTime)
        {
            for (int i = 0; i < Count; i++)
            {
                var entry = this[i];
                if (entry.Time >= fromTime)
                    yield return entry;
            }
        }

        public void Clear()
        {
            _start = 0;
            Count = 0;
        }
    }
}
