using System.Collections.Generic;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// How a PvP match is won.
    /// </summary>
    public enum PvpMode
    {
        /// <summary>
        /// Two sides (one or more players each), no respawn within a round: a side wins a round when the other side
        /// is all down; first to <see cref="PvpSettings.RoundsToWin"/> rounds wins.
        /// </summary>
        Duel = 0,

        /// <summary>
        /// Teams from <see cref="PvpSettings.Teams"/>, players respawn; first team to
        /// <see cref="PvpSettings.ScoreToWin"/> defeats wins.
        /// </summary>
        TeamDeathmatch = 1,

        /// <summary>
        /// Everyone for themselves, players respawn; first player to <see cref="PvpSettings.ScoreToWin"/> defeats wins.
        /// </summary>
        FreeForAll = 2
    }

    /// <summary>
    /// Turns the game into a PvP match (<see cref="ActionRpgSettings.Pvp"/>): players get teams, players of other
    /// teams are hostile, and <see cref="PvpMatch"/> keeps the score. Enemies, if any, still fight every player.
    /// </summary>
    public sealed class PvpSettings
    {
        public PvpMode Mode { get; set; } = PvpMode.TeamDeathmatch;

        /// <summary>
        /// Team names for Duel and TeamDeathmatch; joining players go to the team with fewest players. They can be
        /// faction ids when <see cref="ActionRpgSettings.Hostility"/> is a <see cref="FactionHostility"/>.
        /// Ignored in FreeForAll, where every player is a side of their own ("P1", "P2", ...).
        /// </summary>
        public List<string> Teams { get; } = new List<string> { "Red", "Blue" };

        /// <summary>
        /// Defeats that win TeamDeathmatch and FreeForAll.
        /// </summary>
        public int ScoreToWin { get; set; } = 10;

        /// <summary>
        /// Rounds that win a Duel (2 = best of three).
        /// </summary>
        public int RoundsToWin { get; set; } = 2;

        /// <summary>
        /// Seconds a defeated player waits before coming back (TeamDeathmatch, FreeForAll).
        /// </summary>
        public float RespawnDelay { get; set; } = 3f;

        /// <summary>
        /// Seconds between Duel rounds.
        /// </summary>
        public float RoundBreak { get; set; } = 3f;

        /// <summary>
        /// Seconds a TeamDeathmatch or FreeForAll match lasts at most (0 = no limit); the highest score then wins,
        /// a tie is a draw.
        /// </summary>
        public float TimeLimit { get; set; }

        /// <summary>
        /// Seconds after a match ends before a new one starts (0 = only when <see cref="PvpMatch.Restart"/> is called).
        /// </summary>
        public float RestartDelay { get; set; } = 10f;

        /// <summary>
        /// The side a player plays for in FreeForAll.
        /// </summary>
        public static string FreeForAllSide(int seat) => "P" + (seat + 1);

        /// <summary>
        /// A side's place: its index in <see cref="Teams"/>, or the seat in FreeForAll; -1 when unknown.
        /// </summary>
        public int SideIndexOf(string side)
        {
            if (string.IsNullOrEmpty(side))
                return -1;
            if (Mode == PvpMode.FreeForAll)
                return side.Length > 1 && side[0] == 'P' && int.TryParse(side.Substring(1), out int seat) ? seat - 1 : -1;
            return Teams.IndexOf(side);
        }

        public IEnumerable<string> Validate()
        {
            if (Mode != PvpMode.FreeForAll)
            {
                int named = 0;
                var seen = new HashSet<string>();
                foreach (var team in Teams)
                {
                    if (string.IsNullOrWhiteSpace(team))
                        yield return "PvP: a team has no name.";
                    else if (!seen.Add(team))
                        yield return $"PvP: the team '{team}' is listed twice.";
                    else
                        named++;
                }
                if (named < 2)
                    yield return "PvP: Duel and Team Deathmatch need at least two teams.";
            }
            if (Mode == PvpMode.Duel && RoundsToWin < 1)
                yield return "PvP: Rounds To Win must be at least 1.";
            if (Mode != PvpMode.Duel && ScoreToWin < 1)
                yield return "PvP: Score To Win must be at least 1.";
            if (RespawnDelay < 0f || RoundBreak < 0f || TimeLimit < 0f || RestartDelay < 0f)
                yield return "PvP: delays and the time limit cannot be negative.";
        }
    }
}
