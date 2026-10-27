using System;
using RPGFramework.Abilities;
using RPGFramework.Events;

namespace RPGFramework.Bootstrap.Gameplay
{
    public sealed class AbilitiesOptions
    {
        /// <summary>
        /// Distance between two entities, for ability range; every target is out of range of a ranged ability when null.
        /// </summary>
        public Func<int, int, float> DistanceCheck { get; set; }

        /// <summary>
        /// True when the second entity is an enemy of the first; any entity counts as either when null.
        /// </summary>
        public Func<int, int, bool> RelationshipCheck { get; set; }

        /// <summary>
        /// Maximum of a resource for an entity (entityId, resourceType), for percentage costs.
        /// </summary>
        public Func<int, string, float> ResourceMaximum { get; set; }
    }

    /// <summary>
    /// Registers <see cref="IAbilityRegistry"/>, <see cref="ICooldownTracker"/>, <see cref="ITargetValidator"/> and an
    /// <see cref="IAbilityExecutor"/> using the options' callbacks; events go to the event bus. The game registers its
    /// abilities on the registry when its own module initializes and calls <see cref="IAbilityExecutor.Update"/> with
    /// its clock for cast times and channels.
    /// </summary>
    public sealed class AbilitiesModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.Abilities";

        private readonly AbilitiesOptions _options;

        public AbilitiesModule(AbilitiesOptions options = null)
        {
            _options = options ?? new AbilitiesOptions();
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory<IAbilityRegistry>(_ => new AbilityRegistry());
            Container.RegisterFactory<ICooldownTracker>(c => new CooldownTracker(c.Resolve<IEventPublisher>()));
            Container.RegisterFactory<ITargetValidator>(_ => new TargetValidator());
            Container.RegisterFactory<IAbilityExecutor>(c => new AbilityExecutor(
                c.Resolve<IAbilityRegistry>(),
                c.Resolve<ICooldownTracker>(),
                c.Resolve<ITargetValidator>(),
                c.Resolve<IEventPublisher>(),
                _options.DistanceCheck,
                _options.RelationshipCheck,
                _options.ResourceMaximum));
        }
    }
}
