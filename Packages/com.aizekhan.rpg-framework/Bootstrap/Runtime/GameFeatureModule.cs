using System;
using System.Collections.Generic;
using RPGFramework.Core.Composition;
using RPGFramework.Systems;

namespace RPGFramework.Bootstrap
{
    /// <summary>
    /// Base for game/feature modules that contribute systems.
    /// Declare systems with <see cref="AddSystem{TSystem}"/> inside <see cref="BaseFeatureModule.OnConfigure"/>:
    /// they are registered in the container (so container validation covers their constructors) and added
    /// to the <see cref="ISystemRegistry"/> when the module initializes. Shutdown removes and disposes them.
    /// </summary>
    public abstract class GameFeatureModule : BaseFeatureModule
    {
        private readonly List<PendingSystem> _declared = new List<PendingSystem>();
        private readonly List<ISystem> _systems = new List<ISystem>();

        /// <summary>
        /// Depends on <see cref="FrameworkCoreModule"/>; override and append to add more.
        /// </summary>
        public override IEnumerable<Type> Dependencies => new[] { typeof(FrameworkCoreModule) };

        protected ISystemRegistry Systems { get; private set; }

        /// <summary>
        /// Systems this module added to the registry.
        /// </summary>
        public IReadOnlyList<ISystem> OwnedSystems => _systems;

        protected void AddSystem<TSystem>(int priority = SystemPriorities.Gameplay) where TSystem : class, ISystem
        {
            if (!Container.IsRegistered<TSystem>())
                Container.RegisterSingleton<TSystem, TSystem>();

            if (Systems == null)
                _declared.Add(new PendingSystem(() => Container.Resolve<TSystem>(), priority));
            else
                RegisterNow(Container.Resolve<TSystem>(), priority);
        }

        public sealed override void Initialize()
        {
            Systems = Container.Resolve<ISystemRegistry>();
            foreach (var pending in _declared)
                RegisterNow(pending.Create(), pending.Priority);
            _declared.Clear();

            OnInitialize();
        }

        public sealed override void Shutdown()
        {
            try
            {
                OnShutdown();
            }
            finally
            {
                for (int i = _systems.Count - 1; i >= 0; i--)
                    Systems?.Unregister(_systems[i]);
                _systems.Clear();
            }
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnShutdown()
        {
        }

        private void RegisterNow(ISystem system, int priority)
        {
            if (Systems.Register(system, priority))
                _systems.Add(system);
        }

        private readonly struct PendingSystem
        {
            public PendingSystem(Func<ISystem> create, int priority)
            {
                Create = create;
                Priority = priority;
            }

            public Func<ISystem> Create { get; }
            public int Priority { get; }
        }
    }
}
