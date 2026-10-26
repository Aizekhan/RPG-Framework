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

        private readonly ActorWorld _world;
        private readonly SaveSystem _saveSystem;
        private readonly SaveGameCoordinator _coordinator;
        private readonly string _gameVersion;
        private string _cannotSave;

        public ActionRpgSaves(ActorWorld world, string directory, string gameVersion = "1.0.0")
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("A save directory is required", nameof(directory));
            _gameVersion = gameVersion;
            _saveSystem = new SaveSystem(directory, 1, new JsonSaveSerializer(), new SaveValidator(gameVersion))
            {
                AutoSaveEnabled = false
            };
            _coordinator = new SaveGameCoordinator(_saveSystem).Register(new PlayerProgressSave(world));
        }

        public bool HasSave => _saveSystem.HasSave(Slot);

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
            var player = _world.Player;
            var result = _coordinator.Save(Slot, new SaveMetadata(_gameVersion, player?.DisplayName ?? "", 0f, "", _world.Level));
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

        public bool Delete() => _saveSystem.DeleteSave(Slot);

        private sealed class PlayerProgressSave : SaveParticipant<PlayerProgress>
        {
            private readonly ActorWorld _world;

            public PlayerProgressSave(ActorWorld world) => _world = world;

            public override string SaveKey => "actionrpg.player";

            protected override PlayerProgress CaptureState() => _world.CaptureProgress();

            protected override void RestoreState(PlayerProgress state) => _world.RestoreProgress(state);
        }
    }
}
