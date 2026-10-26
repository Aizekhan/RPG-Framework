using System;
using RPGFramework.ECS;
using RPGFramework.Logging;

namespace RPGFramework.Systems
{
    public enum WorldState
    {
        Created,
        Initialized,
        Disposed
    }

    /// <summary>
    /// Entities plus the systems that process them, ticked as one unit.
    /// Not a singleton: a game may run several worlds (main game, isolated battle simulation, menu preview).
    /// </summary>
    public interface IWorld : IDisposable
    {
        string Name { get; }
        WorldState State { get; }
        IEntityManager Entities { get; }
        ISystemRegistry Systems { get; }

        void Initialize();
        void Update(float deltaTime);
        void FixedUpdate(float fixedDeltaTime);
        void LateUpdate(float deltaTime);
    }

    public sealed class World : IWorld
    {
        private readonly ILogger _logger;

        public World(string name, IEntityManager entities, ISystemRegistry systems, ILogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("World name is required", nameof(name));
            Name = name;
            Entities = entities ?? throw new ArgumentNullException(nameof(entities));
            Systems = systems ?? throw new ArgumentNullException(nameof(systems));
            _logger = logger;
        }

        public string Name { get; }
        public WorldState State { get; private set; } = WorldState.Created;
        public IEntityManager Entities { get; }
        public ISystemRegistry Systems { get; }

        /// <summary>
        /// Total simulated time (sum of Update deltas).
        /// </summary>
        public double Time { get; private set; }

        public long FrameCount { get; private set; }

        public void Initialize()
        {
            switch (State)
            {
                case WorldState.Initialized:
                    return;
                case WorldState.Disposed:
                    throw new ObjectDisposedException($"World '{Name}'");
            }

            Systems.Initialize();
            State = WorldState.Initialized;
            _logger.LogInfo($"World '{Name}' initialized with {Systems.Systems.Count} systems", "World");
        }

        public void Update(float deltaTime)
        {
            if (!CanTick())
                return;
            Time += deltaTime;
            FrameCount++;
            Systems.Update(deltaTime);
        }

        public void FixedUpdate(float fixedDeltaTime)
        {
            if (CanTick())
                Systems.FixedUpdate(fixedDeltaTime);
        }

        public void LateUpdate(float deltaTime)
        {
            if (CanTick())
                Systems.LateUpdate(deltaTime);
        }

        public void Dispose()
        {
            if (State == WorldState.Disposed)
                return;
            State = WorldState.Disposed;
            Systems.Dispose();
            _logger.LogInfo($"World '{Name}' disposed after {FrameCount} frames", "World");
        }

        /// <summary>
        /// Ticking a world that is not initialized is a programming error; ticking after Dispose is
        /// tolerated (engine callbacks may still arrive during teardown).
        /// </summary>
        private bool CanTick()
        {
            switch (State)
            {
                case WorldState.Initialized:
                    return true;
                case WorldState.Disposed:
                    return false;
                default:
                    throw new InvalidOperationException($"World '{Name}' must be initialized before it is updated");
            }
        }
    }
}
