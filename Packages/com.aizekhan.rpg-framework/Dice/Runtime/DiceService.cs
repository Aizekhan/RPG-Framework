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
            return Record(new DieRoll(sides, Next(sides), purpose));
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
    /// Dice for tests: the rolls come in the order given (<see cref="Then"/>), checked against the die's sides; when
    /// they run out it rolls from its seed, or throws with <see cref="StrictlyScripted"/>.
    /// </summary>
    public sealed class ScriptedDice : DiceService
    {
        private readonly Queue<int> _script = new Queue<int>();

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

        public int Remaining => _script.Count;

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

        protected override int Next(int sides)
        {
            if (_script.Count == 0)
            {
                if (StrictlyScripted)
                    throw new InvalidOperationException($"No scripted roll left for a D{sides}");
                return base.Next(sides);
            }
            int value = _script.Dequeue();
            if (value < 1 || value > sides)
                throw new InvalidOperationException($"The scripted roll {value} does not fit a D{sides}");
            return value;
        }
    }
}
