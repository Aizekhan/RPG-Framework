using System;
using RPGFramework.Persistence;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Saves and loads the player's progress with the framework's persistence (<see cref="SaveGameCoordinator"/>):
    /// one save per game in <c>directory</c>. Engine-free; the host passes a folder under the platform's persistent
    /// data path.
    /// </summary>
    public sealed class ActionRpgSaves
    {
        public const string Slot = "autosave";

        private readonly ActorWorld _world;
        private readonly SaveSystem _saveSystem;
        private readonly SaveGameCoordinator _coordinator;
        private readonly string _gameVersion;

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

        /// <returns>Null on success, otherwise why it failed.</returns>
        public string Save()
        {
            var player = _world.Player;
            var result = _coordinator.Save(Slot, new SaveMetadata(_gameVersion, player?.DisplayName ?? "", 0f, "", _world.Level));
            return result.Success ? null : result.ErrorMessage;
        }

        /// <returns>Null on success (or when there is nothing to load), otherwise why it failed.</returns>
        public string Load()
        {
            if (!HasSave)
                return null;
            var result = _coordinator.Load(Slot);
            return result.Success ? null : result.ErrorMessage;
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
