using System;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Persistence;

namespace RPGFramework.FogOfWar
{
    /// <summary>
    /// Keeps what each team explored in a save (section "fog-of-war"; give every level's map its own key). Register it
    /// with the game's saves (<c>ActionRpgSaves.Register</c>, <see cref="SaveGameCoordinator.Register"/>). A record of
    /// another map size is skipped.
    /// </summary>
    public sealed class FogOfWarSave : SaveParticipant<FogOfWarRecord>
    {
        public const string Key = "fog-of-war";

        private readonly Func<FogOfWarMap> _map;

        public FogOfWarSave(FogOfWarMap map, string key = Key)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            _map = () => map;
            SaveKey = string.IsNullOrEmpty(key) ? throw new ArgumentException("Needs a key", nameof(key)) : key;
        }

        /// <summary>
        /// For the map of the moment (the level being played).
        /// </summary>
        public FogOfWarSave(Func<FogOfWarMap> map, string key = Key)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            SaveKey = string.IsNullOrEmpty(key) ? throw new ArgumentException("Needs a key", nameof(key)) : key;
        }

        public override string SaveKey { get; }

        protected override FogOfWarRecord CaptureState() => _map()?.Capture() ?? new FogOfWarRecord();

        protected override void RestoreState(FogOfWarRecord state)
        {
            if (state != null)
                _map()?.Restore(state);
        }
    }

    /// <summary>
    /// Registers one <see cref="FogOfWarMap"/> (announced on the bus) for a game with one map. A game with a map per
    /// level makes a <see cref="FogOfWarMap"/> per level instead. A map registered before this module configures is kept.
    /// </summary>
    public sealed class FogOfWarModule : GameFeatureModule
    {
        public const string ModuleName = "Framework.FogOfWar";

        private readonly int _width;
        private readonly int _height;
        private readonly ISightBlockers _blockers;
        private readonly int _teams;

        public FogOfWarModule(int width, int height, ISightBlockers blockers = null, int teams = 1)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");
            if (teams < 1 || teams > FogOfWarMap.MaxTeams)
                throw new ArgumentOutOfRangeException(nameof(teams), $"Teams must be 1..{FogOfWarMap.MaxTeams}");
            _width = width;
            _height = height;
            _blockers = blockers;
            _teams = teams;
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            if (!Container.IsRegistered<FogOfWarMap>())
                Container.RegisterFactory<FogOfWarMap>(c => new FogOfWarMap(_width, _height, _blockers, _teams,
                    c.IsRegistered<IEventPublisher>() ? c.Resolve<IEventPublisher>() : null));
        }
    }
}
