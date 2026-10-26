using System;
using RPGFramework.Core.Composition;
using RPGFramework.DI;
using RPGFramework.Logging;
using RPGFramework.Systems;

namespace RPGFramework.Bootstrap
{
    public enum GameHostState
    {
        Created,
        Running,
        Stopped,
        Failed
    }

    /// <summary>
    /// Engine-independent game entry point:
    /// container → <see cref="FrameworkCoreModule"/> + game modules → validation → module initialization
    /// → world initialization, then per-frame ticking and ordered shutdown
    /// (world/systems first, then modules in reverse dependency order).
    /// A Unity scene drives it through <c>GameHostBehaviour</c>; tests and servers drive it directly.
    /// </summary>
    public sealed class GameHost : IDisposable
    {
        private const string Category = "Bootstrap";

        private readonly FrameworkCoreOptions _options;

        public GameHost(ILogger logger = null, FrameworkCoreOptions options = null)
        {
            Logger = logger ?? NullLogger.Instance;
            _options = options ?? new FrameworkCoreOptions();
        }

        public ILogger Logger { get; }
        public GameHostState State { get; private set; } = GameHostState.Created;
        public IDIContainer Container { get; private set; }
        public IGameComposition Composition { get; private set; }
        public IWorld World { get; private set; }

        /// <summary>
        /// Builds and starts the game. On failure everything already started is shut down,
        /// <see cref="State"/> becomes <see cref="GameHostState.Failed"/> and the exception is rethrown.
        /// </summary>
        /// <param name="configure">Adds game modules (and may disable container validation).</param>
        /// <param name="container">Optional pre-populated container; a new one is created otherwise.</param>
        public void Start(Action<IGameCompositionBuilder> configure, IDIContainer container = null)
        {
            if (State != GameHostState.Created)
                throw new InvalidOperationException($"GameHost can only be started once (state: {State})");

            try
            {
                Container = container ?? new DIContainer(Logger);
                Container.RegisterInstance(Logger);

                var builder = new GameCompositionBuilder(Container, Logger);
                builder.UseModule(new FrameworkCoreModule(_options));
                configure?.Invoke(builder);

                Composition = builder.Build();
                World = Container.Resolve<IWorld>();
                World.Initialize();

                State = GameHostState.Running;
                Logger.LogInfo($"Game started with {Composition.Modules.Count} modules", Category);
            }
            catch (Exception ex)
            {
                State = GameHostState.Failed;
                Logger.LogCritical("Game failed to start", Category, ex);
                ShutdownSafely();
                throw;
            }
        }

        public void Update(float deltaTime)
        {
            if (State == GameHostState.Running)
                World.Update(deltaTime);
        }

        public void FixedUpdate(float fixedDeltaTime)
        {
            if (State == GameHostState.Running)
                World.FixedUpdate(fixedDeltaTime);
        }

        public void LateUpdate(float deltaTime)
        {
            if (State == GameHostState.Running)
                World.LateUpdate(deltaTime);
        }

        /// <summary>
        /// Stops the game. Safe to call in any state and more than once.
        /// </summary>
        public void Dispose()
        {
            if (State != GameHostState.Running)
                return;

            State = GameHostState.Stopped;
            ShutdownSafely();
            Logger.LogInfo("Game stopped", Category);
        }

        private void ShutdownSafely()
        {
            try
            {
                World?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError("World dispose failed", Category, ex);
            }

            Composition?.Shutdown();
        }
    }
}
