using System;
using RPGFramework.Persistence;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// Saves and loads the run with the framework's persistence (<see cref="SaveGameCoordinator"/>): one save per game
    /// in <c>directory</c>. Engine-free; the host passes a folder under the platform's persistent data path. As with
    /// the action RPG saves, a save is never lost to a bad load: a damaged latest save falls back to the previous one,
    /// one that cannot be read at all is moved aside before a fresh start, and a save made by a newer build is not
    /// saved over. A battle in progress is not saved: loading puts the party before the next encounter.
    /// </summary>
    public sealed class TurnBasedSaves
    {
        public const string Slot = "autosave";

        private readonly TurnBasedWorld _world;
        private readonly SaveSystem _saveSystem;
        private readonly SaveGameCoordinator _coordinator;
        private readonly string _gameVersion;
        private string _cannotSave;

        public TurnBasedSaves(TurnBasedWorld world, string directory, string gameVersion = "1.0.0")
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("A save directory is required", nameof(directory));
            _gameVersion = gameVersion;
            _saveSystem = new SaveSystem(directory, 1, new JsonSaveSerializer(), new SaveValidator(gameVersion))
            {
                AutoSaveEnabled = false
            };
            _coordinator = new SaveGameCoordinator(_saveSystem).Register(new PartyProgressSave(world));
        }

        public bool HasSave => _saveSystem.HasSave(Slot);

        /// <summary>
        /// Adds the game's own state to the save, in its own section. Register before <see cref="Load"/>.
        /// </summary>
        public TurnBasedSaves Register(ISaveParticipant participant)
        {
            _coordinator.Register(participant);
            return this;
        }

        /// <summary>
        /// What the last load had to do that the player should know; empty when it went normally.
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
            var leader = _world.Party.Count > 0 ? _world.Party[0] : null;
            var metadata = new SaveMetadata(_gameVersion, leader?.Name ?? "", 0f,
                _world.NextEncounter?.DisplayName ?? "", leader?.Level ?? 1);
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

        private sealed class PartyProgressSave : SaveParticipant<PartyProgress>
        {
            private readonly TurnBasedWorld _world;

            public PartyProgressSave(TurnBasedWorld world) => _world = world;

            public override string SaveKey => "turnbased.party";

            protected override PartyProgress CaptureState() => _world.CaptureProgress();

            protected override void RestoreState(PartyProgress state) => _world.RestoreProgress(state);
        }
    }
}
