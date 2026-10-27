using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.StatusEffects;

namespace RPGFramework.Bootstrap.Gameplay
{
    public sealed class StatusEffectsOptions
    {
        /// <summary>
        /// Where stat modifiers of effects go; an <see cref="IStatusEffectTarget"/> registered in the container is
        /// used when null, and modifiers are only tracked when there is neither.
        /// </summary>
        public IStatusEffectTarget Target { get; set; }
    }

    /// <summary>
    /// Registers <see cref="IStatusEffectManager"/> over the core entity manager; applied, removed and expired effects
    /// are published on the event bus. The game registers its effect definitions on the manager when its own module
    /// initializes and calls <see cref="IStatusEffectManager.UpdateEffects"/> with its own measure of time (seconds,
    /// turns).
    /// </summary>
    public sealed class StatusEffectsModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.StatusEffects";

        private readonly StatusEffectsOptions _options;

        public StatusEffectsModule(StatusEffectsOptions options = null)
        {
            _options = options ?? new StatusEffectsOptions();
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory<IStatusEffectManager>(c => new StatusEffectManager(
                c.Resolve<IEntityManager>(),
                c.Resolve<IEventPublisher>(),
                _options.Target ?? (c.IsRegistered<IStatusEffectTarget>() ? c.Resolve<IStatusEffectTarget>() : null)));
        }
    }
}
