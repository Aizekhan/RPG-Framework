using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Dice
{
    /// <summary>
    /// The rules' source of chance: dice from a seed with a log of every roll, so a match can be replayed from its
    /// seed and its moves, and tested with <see cref="ScriptedDice"/>. Decks shuffle with it; services that take a seed
    /// (a loot roll) take <see cref="NextSeed"/>. Registered by <c>FrameworkCoreModule</c> (seed in its options).
    /// </summary>
    public interface IDiceService
    {
        /// <summary>
        /// Seed the dice started from (the last <see cref="Reseed"/>).
        /// </summary>
        int Seed { get; }

        /// <summary>
        /// Every roll since the start or <see cref="ClearLog"/>, oldest first.
        /// </summary>
        IReadOnlyList<DieRoll> Log { get; }

        /// <summary>
        /// Rolls one die: 1..<paramref name="sides"/>. <paramref name="purpose"/> says what for ("move", "fight");
        /// <see cref="DicePurposes.Shuffle"/> rolls are logged but not announced.
        /// </summary>
        int Roll(int sides, string purpose = null);

        /// <summary>
        /// A seed for a service that takes one, drawn from the dice (the same seed gives the same seeds).
        /// </summary>
        int NextSeed();

        /// <summary>
        /// Starts the dice over from a seed (a new or a loaded match). The log stays.
        /// </summary>
        void Reseed(int seed);

        void ClearLog();
    }

    /// <summary>
    /// Purposes the framework rolls for itself.
    /// </summary>
    public static class DicePurposes
    {
        /// <summary>
        /// A deck being shuffled: logged, not announced.
        /// </summary>
        public const string Shuffle = "shuffle";
    }

    /// <summary>
    /// One die rolled: its sides, the value and what it was for.
    /// </summary>
    public readonly struct DieRoll : IEquatable<DieRoll>
    {
        public DieRoll(int sides, int value, string purpose)
        {
            Sides = sides;
            Value = value;
            Purpose = purpose;
        }

        public int Sides { get; }
        public int Value { get; }
        public string Purpose { get; }

        public bool Equals(DieRoll other) => Sides == other.Sides && Value == other.Value && Purpose == other.Purpose;
        public override bool Equals(object obj) => obj is DieRoll other && Equals(other);
        public override int GetHashCode() => (Sides, Value, Purpose).GetHashCode();
        public override string ToString() => $"D{Sides}={Value}" + (string.IsNullOrEmpty(Purpose) ? "" : $" ({Purpose})");
    }

    /// <summary>
    /// A die was rolled (not for a shuffle): dice views, a roll history, sounds.
    /// </summary>
    public readonly struct DieRolledEvent : IEvent
    {
        public DieRolledEvent(DieRoll roll)
        {
            Roll = roll;
        }

        public DieRoll Roll { get; }
    }
}
