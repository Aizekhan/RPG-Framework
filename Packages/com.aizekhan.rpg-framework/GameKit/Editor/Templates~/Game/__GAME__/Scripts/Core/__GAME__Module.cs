using RPGFramework.Bootstrap;
using RPGFramework.Events;

namespace __GAME__
{
    /// <summary>
    /// The game's own module: registers the session and starts it once every module is initialized.
    /// </summary>
    public sealed class __GAME__Module : GameFeatureModule
    {
        public const string ModuleName = "__GAME__";

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            Container.RegisterFactory(c => new __GAME__Session(c.Resolve<IEventPublisher>()));
        }

        protected override void OnInitialize()
        {
            Container.Resolve<__GAME__Session>().Start();
        }
    }
}
