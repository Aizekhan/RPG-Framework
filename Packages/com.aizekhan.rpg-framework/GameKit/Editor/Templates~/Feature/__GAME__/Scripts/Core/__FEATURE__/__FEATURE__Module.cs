using RPGFramework.Bootstrap;
using RPGFramework.Events;

namespace __GAME__
{
    /// <summary>
    /// Registers the __FEATURE__ rules. Added to the game in __GAME__Composition.
    /// Depends on FrameworkCoreModule; override Dependencies to add the modules this feature uses.
    /// </summary>
    public sealed class __FEATURE__Module : GameFeatureModule
    {
        public const string ModuleName = "__GAME__.__FEATURE__";

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory(c => new __FEATURE__Rules(c.Resolve<IEventPublisher>()));
        }
    }
}
