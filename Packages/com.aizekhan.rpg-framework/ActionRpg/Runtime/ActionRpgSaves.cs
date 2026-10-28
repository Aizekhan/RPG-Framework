using System;
using RPGFramework.Persistence;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Saves and loads the player's progress with the framework's persistence (<see cref="SaveGameCoordinator"/>):
    /// one save per game in <c>directory</c>. Engine-free; the host passes a folder under the platform's persistent
    /// data path. A save is never lost to a bad load: a damaged latest save falls back to the previous one; one that
    /// cannot be read at all is moved aside (renamed "unreadable" with the time) before a fresh start; and a save made by
    /// a newer build is left alone: nothing is saved over it.
    /// </summary>
    public sealed class ActionRpgSaves
    {
        public const string Slot = "autosave";
        private const string LoadoutKey = "loadout";

        private readonly Func<PlayerProgress> _capture;
        private readonly Func<string> _playerName;
        private readonly Func<int> _level;
        private readonly SaveSystem _saveSystem;
        private readonly SaveGameCoordinator _coordinator;
        private readonly string _gameVersion;
        private string _cannotSave;

        public ActionRpgSaves(ActorWorld world, string directory, string gameVersion = "1.0.0")
            : this(directory, gameVersion, CheckWorld(world).CaptureProgress, world.RestoreProgress,
                () => world.Player?.DisplayName ?? "", () => world.Level)
        {
        }

        /// <summary>
        /// Saves of a player whose game runs elsewhere: a co-op guest with progress of their own
        /// (<see cref="ProgressMode.PerPlayer"/>) loads it to bring along and saves what the host reports back. Only
        /// the player's progress is written; the other sections of the save on disk are kept as they are.
        /// </summary>
        /// <param name="capture">The progress to save; null skips the save (nothing to save yet).</param>
        /// <param name="restore">Receives the loaded progress.</param>
        public static ActionRpgSaves ForProgressOnly(string directory, string gameVersion, Func<PlayerProgress> capture,
            Action<PlayerProgress> restore)
        {
            var saves = new ActionRpgSaves(directory, gameVersion, capture, restore, () => "", () => capture()?.Level ?? 1);
            saves._coordinator.KeepUnknownSections = true;
            return saves;
        }

        private ActionRpgSaves(string directory, string gameVersion, Func<PlayerProgress> capture,
            Action<PlayerProgress> restore, Func<string> playerName, Func<int> level)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("A save directory is required", nameof(directory));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            if (restore == null)
                throw new ArgumentNullException(nameof(restore));
            _playerName = playerName;
            _level = level;
            _gameVersion = gameVersion;
            _saveSystem = new SaveSystem(directory, 1, new JsonSaveSerializer(), new SaveValidator(gameVersion))
            {
                AutoSaveEnabled = false
            };
            _coordinator = new SaveGameCoordinator(_saveSystem).Register(new PlayerProgressSave(capture, restore));
        }

        private static ActorWorld CheckWorld(ActorWorld world) => world ?? throw new ArgumentNullException(nameof(world));

        public bool HasSave => _saveSystem.HasSave(Slot);

        /// <summary>
        /// Seconds played with this save, carried on from the loaded one; the host adds the time played.
        /// </summary>
        public float PlaySeconds { get; set; }

        /// <summary>
        /// Where the player is (a scene, a floor), shown in <see cref="SaveSummary.Location"/>.
        /// </summary>
        public string Location { get; set; } = "";

        /// <summary>
        /// What a start menu shows of the save in <paramref name="directory"/> (level, name, hero, time played, when)
        /// without starting a game; null when there is none or it cannot be read.
        /// </summary>
        /// <param name="gameVersion">This build's version, to tell a save made by a newer one.</param>
        public static SaveSummary ReadSummary(string directory, string gameVersion = null)
        {
            if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
                return null;
            var system = new SaveSystem(directory, 1, new JsonSaveSerializer(), new SaveValidator(gameVersion ?? "1.0.0"))
            {
                AutoSaveEnabled = false
            };
            if (!system.HasSave(Slot))
                return null;
            var metadata = system.GetMetadata(Slot);
            return metadata == null ? null : new SaveSummary(metadata, gameVersion);
        }

        /// <summary>
        /// Adds the game's own state to the save (a shop's coins, opened chests): it is saved and loaded with the
        /// player's progress, in its own section. Register before <see cref="Load"/>.
        /// </summary>
        public ActionRpgSaves Register(ISaveParticipant participant)
        {
            _coordinator.Register(participant);
            return this;
        }

        /// <summary>
        /// What the last load had to do that the player should know (loaded the previous save, kept an unreadable one
        /// aside); empty when it went normally.
        /// </summary>
        public string Notice { get; private set; } = "";

        /// <summary>
        /// Saving is off because the save on disk was made by a newer build.
        /// </summary>
        public bool CanSave => _cannotSave == null;

        /// <returns>Null on success, otherwise why it failed.</returns>
        public string Save()
        {
            if (_cannotSave != null)
                return _cannotSave;
            // Nothing to save yet (a guest before the host reported their progress)
            var progress = _capture();
            if (progress == null)
                return null;
            var metadata = new SaveMetadata(_gameVersion, _playerName(), PlaySeconds, Location ?? "", _level());
            metadata.CustomData[LoadoutKey] = progress.Loadout ?? "";
            var result = _coordinator.Save(Slot, metadata);
            return result.Success ? null : result.ErrorMessage;
        }

        /// <returns>Null on success (or when there is nothing to load), otherwise why it failed.</returns>
        public string Load()
        {
            Notice = "";
            if (!HasSave)
                return null;
            var result = _coordinator.Load(Slot);
            if (result.Success)
            {
                var metadata = _saveSystem.GetMetadata(Slot);
                PlaySeconds = metadata?.PlaytimeSeconds ?? 0f;
                Location = metadata?.LocationName ?? "";
                if (result.FromPrevious)
                    Notice = "The latest save could not be read, so the one before it was loaded.";
                return null;
            }
            if (result.Error == SaveError.IncompatibleVersion)
            {
                _cannotSave = "Progress is not saved: the save on disk was made by a newer version of the game and is kept as it is.";
                return $"{result.ErrorMessage}. {_cannotSave}";
            }
            string kept = _saveSystem.SetAside(Slot);
            Notice = kept == null
                ? "The save could not be read; starting fresh."
                : $"The save could not be read; it was kept as {System.IO.Path.GetFileName(kept)} and the game starts fresh.";
            return $"{result.ErrorMessage}. {Notice}";
        }

        public bool Delete()
        {
            PlaySeconds = 0f;
            return _saveSystem.DeleteSave(Slot);
        }

        internal static string LoadoutOf(ISaveMetadata metadata) =>
            metadata.CustomData != null && metadata.CustomData.TryGetValue(LoadoutKey, out string loadout) ? loadout ?? "" : "";

        private sealed class PlayerProgressSave : SaveParticipant<PlayerProgress>
        {
            private readonly Func<PlayerProgress> _capture;
            private readonly Action<PlayerProgress> _restore;

            public PlayerProgressSave(Func<PlayerProgress> capture, Action<PlayerProgress> restore)
            {
                _capture = capture;
                _restore = restore;
            }

            public override string SaveKey => "actionrpg.player";
            protected override PlayerProgress CaptureState() => _capture();
            protected override void RestoreState(PlayerProgress state) => _restore(state);
        }
    }
}
