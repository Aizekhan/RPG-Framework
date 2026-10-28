using RPGFramework.Events;
using RPGFramework.Interaction;

namespace RPGFramework.Bootstrap.Gameplay
{
    /// <summary>
    /// Registers <see cref="IInteractionService"/>: the places players use (chests, doors, levers, traders) and the
    /// nearest one per player; <see cref="InteractedEvent"/> goes to the event bus. The game adds its places and
    /// sets <see cref="IInteractionService.Distance"/> to its own measure, and reacts to the event in its module.
    /// </summary>
    public sealed class InteractionModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.Interaction";

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory<IInteractionService>(c => new InteractionService(c.Resolve<IEventPublisher>()));
        }
    }
}
