using System;
using System.Collections.Generic;
using System.IO;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A PvP match as the HUD shows it: the same on the host (from <see cref="PvpMatch"/>) and on a joined client
    /// (sent by the host).
    /// </summary>
    public sealed class PvpStatus
    {
        public struct Side
        {
            public string Name;
            public int Score;
            public int RoundWins;
        }

        public PvpMode Mode;
        public PvpPhase Phase;
        public int Round;

        /// <summary>
        /// The winner once finished; null on a draw or before the end.
        /// </summary>
        public string Winner;

        /// <summary>
        /// Whole seconds left of a time-limited match; 0 without a limit.
        /// </summary>
        public int SecondsLeft;

        public List<Side> Sides = new List<Side>();

        public int ScoreOf(string side)
        {
            foreach (var entry in Sides)
            {
                if (entry.Name == side)
                    return entry.Score;
            }
            return 0;
        }

        /// <summary>
        /// Fills this status from a match (reusing the list, so polling it every frame makes no garbage).
        /// </summary>
        public void CopyFrom(PvpMatch match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));
            Mode = match.Settings?.Mode ?? PvpMode.TeamDeathmatch;
            Phase = match.Phase;
            Round = match.Round;
            Winner = match.Winner;
            SecondsLeft = (int)Math.Ceiling(match.TimeLeft);
            Sides.Clear();
            foreach (var side in match.Sides)
                Sides.Add(new Side { Name = side, Score = match.ScoreOf(side), RoundWins = match.RoundWinsOf(side) });
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)Mode);
            writer.Write((byte)Phase);
            writer.Write(Round);
            writer.Write(Winner ?? "");
            writer.Write(SecondsLeft);
            writer.Write(Sides.Count);
            foreach (var side in Sides)
            {
                writer.Write(side.Name ?? "");
                writer.Write(side.Score);
                writer.Write(side.RoundWins);
            }
        }

        /// <param name="readCount">Reads an entry count, refusing one no real message has.</param>
        public void Read(BinaryReader reader, Func<BinaryReader, int> readCount)
        {
            Mode = (PvpMode)reader.ReadByte();
            Phase = (PvpPhase)reader.ReadByte();
            Round = reader.ReadInt32();
            string winner = reader.ReadString();
            Winner = winner.Length > 0 ? winner : null;
            SecondsLeft = reader.ReadInt32();
            int count = readCount(reader);
            Sides = new List<Side>(count);
            for (int i = 0; i < count; i++)
                Sides.Add(new Side { Name = reader.ReadString(), Score = reader.ReadInt32(), RoundWins = reader.ReadInt32() });
        }
    }
}
