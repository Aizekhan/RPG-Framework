using System;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Persistence;

namespace RPGFramework.WorldMap
{
    /// <summary>
    /// Keeps the local player's place and discoveries in a save (section "world-map"). Player ids change between play
    /// sessions, so it saves whoever <c>playerId</c> names now and restores onto the id of that moment. Register it with
    /// the game's saves (<c>ActionRpgSaves.Register</c>, <see cref="SaveGameCoordinator.Register"/>).
    /// </summary>
    public sealed class WorldTravelSave : SaveParticipant<WorldTravelRecord>
    {
        public const string Key = "world-map";

        private readonly IWorldTravel _travel;
        private readonly Func<int> _playerId;

        public WorldTravelSave(IWorldTravel travel, Func<int> playerId)
        {
            _travel = travel ?? throw new ArgumentNullException(nameof(travel));
            _playerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
        }

        public override string SaveKey => Key;

        protected override WorldTravelRecord CaptureState() => _travel.Capture(_playerId());

        protected override void RestoreState(WorldTravelRecord state) => _travel.Restore(_playerId(), state ?? new WorldTravelRecord());
    }

    /// <summary>
    /// Registers <see cref="WorldGraph"/> and <see cref="IWorldTravel"/> (announced on the bus) for the game's map.
    /// A travel service registered before this module configures is kept.
    /// </summary>
    public sealed class WorldMapModule : GameFeatureModule
    {
        public const string ModuleName = "Framework.WorldMap";

        private readonly WorldGraph _map;

        public WorldMapModule(WorldGraph map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            if (!Container.IsRegistered<WorldGraph>())
                Container.RegisterInstance(_map);
            if (!Container.IsRegistered<IWorldTravel>())
                Container.RegisterFactory<IWorldTravel>(c => new WorldTravel(c.Resolve<WorldGraph>(),
                    c.IsRegistered<IEventPublisher>() ? c.Resolve<IEventPublisher>() : null));
        }
    }
}
