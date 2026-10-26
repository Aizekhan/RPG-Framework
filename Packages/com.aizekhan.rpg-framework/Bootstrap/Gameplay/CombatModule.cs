using RPGFramework.Combat;
using RPGFramework.Events;

namespace RPGFramework.Bootstrap.Gameplay
{
    /// <summary>
    /// Registers <see cref="ILifeStateManager"/> (deaths and resurrections are also published on the event bus)
    /// and <see cref="IDamageCalculator"/>.
    /// </summary>
    public sealed class CombatModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.Combat";

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory<ILifeStateManager>(c => new LifeStateManager(c.Resolve<IEventPublisher>()));
            Container.RegisterFactory<IDamageCalculator>(_ => new DamageCalculator());
        }
    }
}
