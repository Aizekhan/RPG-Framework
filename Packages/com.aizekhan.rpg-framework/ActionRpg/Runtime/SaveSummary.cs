using System;
using RPGFramework.Persistence;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A save as a start menu shows it ("Continue: Ayla, level 7, 3 h 12 min, yesterday"), read without starting the
    /// game (<see cref="ActionRpgSaves.ReadSummary"/>).
    /// </summary>
    public sealed class SaveSummary
    {
        internal SaveSummary(ISaveMetadata metadata, string gameVersion)
        {
            PlayerName = metadata.CharacterName ?? "";
            Level = metadata.CharacterLevel;
            Loadout = ActionRpgSaves.LoadoutOf(metadata);
            PlaySeconds = metadata.PlaytimeSeconds;
            Location = metadata.LocationName ?? "";
            SavedAt = DateTimeOffset.FromUnixTimeSeconds(metadata.Timestamp);
            GameVersion = metadata.GameVersion ?? "";
            MadeByNewerVersion = IsNewer(GameVersion, gameVersion);
        }

        public string PlayerName { get; }
        public int Level { get; }

        /// <summary>
        /// The set the player wore (<see cref="PlayerProgress.Loadout"/>): the hero, in games with a set per hero.
        /// </summary>
        public string Loadout { get; }

        public float PlaySeconds { get; }
        public string Location { get; }
        public DateTimeOffset SavedAt { get; }
        public string GameVersion { get; }

        /// <summary>
        /// A newer build made it: it loads as far as it can, but nothing is saved over it.
        /// </summary>
        public bool MadeByNewerVersion { get; }

        private static bool IsNewer(string saved, string current) =>
            Version.TryParse(saved, out var a) && Version.TryParse(current, out var b) && a > b;
    }
}
