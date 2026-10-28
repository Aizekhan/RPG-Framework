using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Dice
{
    /// <summary>
    /// Dice from a seed: the same seed gives the same rolls on every machine and every run. Publishes
    /// <see cref="DieRolledEvent"/> for every roll that is not a shuffle.
    /// </summary>
    public class DiceService : IDiceService
    {
        private readonly IEventPublisher _events;
        private readonly List<DieRoll> _log = new List<DieRoll>();
        private Random _random;

        public DiceService(int seed, IEventPublisher events = null)
        {
            _events = events;
            Reseed(seed);
        }

        public int Seed { get; private set; }

        public IReadOnlyList<DieRoll> Log => _log;

        /// <summary>
        /// Rolls kept in the log at most (the oldest go); 0 keeps every one.
        /// </summary>
        public int LogLimit { get; set; }

        public int Roll(int sides, string purpose = null)
        {
            if (sides < 1)
                throw new ArgumentOutOfRangeException(nameof(sides), sides, "A die has at least one side");
            return Record(new DieRoll(sides, Next(sides, purpose), purpose));
        }

        public int NextSeed() => _random.Next();

        public void Reseed(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public void ClearLog() => _log.Clear();

        /// <summary>
        /// The value of a die of <paramref name="sides"/> sides (1..sides), before it is logged.
        /// </summary>
        protected virtual int Next(int sides) => _random.Next(1, sides + 1);

        /// <summary>
        /// The value of a die rolled for <paramref name="purpose"/>; by default the same as <see cref="Next(int)"/>.
        /// </summary>
        protected virtual int Next(int sides, string purpose) => Next(sides);

        private int Record(DieRoll roll)
        {
            _log.Add(roll);
            if (LogLimit > 0 && _log.Count > LogLimit)
                _log.RemoveRange(0, _log.Count - LogLimit);
            if (roll.Purpose != DicePurposes.Shuffle)
                _events?.Publish(new DieRolledEvent(roll));
            return roll.Value;
        }
    }

    /// <summary>
    /// Dice for tests: the rolls come in the order given (<see cref="Then(int[])"/>), checked against the die's sides;
    /// rolls scripted for a purpose (<see cref="Then(string, int[])"/>) go only to rolls for it and come first. When
    /// they run out it rolls from its seed, or throws with <see cref="StrictlyScripted"/>.
    /// </summary>
    public sealed class ScriptedDice : DiceService
    {
        private readonly Queue<int> _script = new Queue<int>();
        private readonly Dictionary<string, Queue<int>> _byPurpose = new Dictionary<string, Queue<int>>();

        public ScriptedDice(params int[] rolls) : this(0, null, rolls)
        {
        }

        public ScriptedDice(int seed, IEventPublisher events, params int[] rolls) : base(seed, events)
        {
            Then(rolls);
        }

        /// <summary>
        /// Throw instead of rolling when the script has run out.
        /// </summary>
        public bool StrictlyScripted { get; set; }

        /// <summary>
        /// Scripted rolls not rolled yet, those for a purpose included.
        /// </summary>
        public int Remaining
        {
            get
            {
                int count = _script.Count;
                foreach (var queue in _byPurpose.Values)
                    count += queue.Count;
                return count;
            }
        }

        /// <summary>
        /// Scripted rolls for <paramref name="purpose"/> not rolled yet.
        /// </summary>
        public int RemainingFor(string purpose) =>
            purpose != null && _byPurpose.TryGetValue(purpose, out var queue) ? queue.Count : 0;

        /// <summary>
        /// The next rolls, in order.
        /// </summary>
        public ScriptedDice Then(params int[] rolls)
        {
            if (rolls != null)
            {
                foreach (int roll in rolls)
                    _script.Enqueue(roll);
            }
            return this;
        }

        /// <summary>
        /// The next rolls for <paramref name="purpose"/> ("fight"), in order; other rolls take the plain script.
        /// </summary>
        public ScriptedDice Then(string purpose, params int[] rolls)
        {
            if (purpose == null)
                throw new ArgumentNullException(nameof(purpose));
            if (rolls == null)
                return this;
            if (!_byPurpose.TryGetValue(purpose, out var queue))
                _byPurpose[purpose] = queue = new Queue<int>();
            foreach (int roll in rolls)
                queue.Enqueue(roll);
            return this;
        }

        protected override int Next(int sides, string purpose)
        {
            if (purpose != null && _byPurpose.TryGetValue(purpose, out var queue) && queue.Count > 0)
                return Check(queue.Dequeue(), sides, purpose);
            return Next(sides);
        }

        protected override int Next(int sides)
        {
            if (_script.Count == 0)
            {
                if (StrictlyScripted)
                    throw new InvalidOperationException($"No scripted roll left for a D{sides}");
                return base.Next(sides);
            }
            return Check(_script.Dequeue(), sides, null);
        }

        private static int Check(int value, int sides, string purpose)
        {
            if (value < 1 || value > sides)
            {
                string what = purpose == null ? "" : " for " + purpose;
                throw new InvalidOperationException($"The scripted roll {value}{what} does not fit a D{sides}");
            }
            return value;
        }
    }
}
