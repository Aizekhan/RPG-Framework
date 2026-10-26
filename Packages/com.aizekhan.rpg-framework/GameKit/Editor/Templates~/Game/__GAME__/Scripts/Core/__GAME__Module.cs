using System;
using System.Collections.Generic;
using RPGFramework.ActionRpg;
using RPGFramework.Bootstrap;
using RPGFramework.Events;

namespace __GAME__
{
    /// <summary>
    /// The game's own module: registers the session and starts it once the action RPG world is running.
    /// </summary>
    public sealed class __GAME__Module : GameFeatureModule
    {
        public const string ModuleName = "__GAME__";

        public override string Name => ModuleName;

        public override IEnumerable<Type> Dependencies => new[] { typeof(ActionRpgModule) };

        protected override void OnConfigure()
        {
            Container.RegisterFactory(c => new __GAME__Session(c.Resolve<IEventBus>()));
        }

        protected override void OnInitialize()
        {
            Container.Resolve<__GAME__Session>().Start();
        }

        protected override void OnShutdown()
        {
            Container.Resolve<__GAME__Session>().Dispose();
        }
    }
}
