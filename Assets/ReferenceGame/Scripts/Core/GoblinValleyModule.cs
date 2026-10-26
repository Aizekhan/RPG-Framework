using System;
using System.Collections.Generic;
using RPGFramework.ActionRpg;
using RPGFramework.Bootstrap;
using RPGFramework.Core.Composition;
using RPGFramework.Events;

namespace ReferenceGame
{
    /// <summary>
    /// Goblin Valley's own module on top of the action RPG: the kill streak rule.
    /// </summary>
    public sealed class GoblinValleyModule : GameFeatureModule
    {
        public const string ModuleName = "ReferenceGame.GoblinValley";

        private readonly KillStreakSettings _killStreak;

        public GoblinValleyModule(KillStreakSettings killStreak = null)
        {
            _killStreak = killStreak ?? new KillStreakSettings();
        }

        public override string Name => ModuleName;

        public override IEnumerable<Type> Dependencies => new[] { typeof(ActionRpgModule) };

        protected override void OnConfigure()
        {
            Container.RegisterFactory(c => new KillStreak(c.Resolve<ActorWorld>(), c.Resolve<IEventBus>(), _killStreak));
        }

        protected override void OnInitialize()
        {
            Container.Resolve<KillStreak>().Start();
        }

        protected override void OnShutdown()
        {
            Container.Resolve<KillStreak>().Dispose();
        }
    }

    /// <summary>
    /// The game's modules, shared by the scene host and the tests so both run the same game.
    /// </summary>
    public static class GoblinValleyComposition
    {
        public static void Configure(IGameCompositionBuilder builder, KillStreakSettings killStreak = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            builder.UseModule(new GoblinValleyModule(killStreak));
        }

        /// <summary>
        /// The whole game from settings and content (what the host does with the Game Setup asset).
        /// </summary>
        public static ActionRpgModule ConfigureWithActionRpg(IGameCompositionBuilder builder, ActionRpgSettings settings,
            ActionRpgContent content, KillStreakSettings killStreak = null)
        {
            var actionRpg = ActionRpgComposition.Configure(builder, settings, content);
            Configure(builder, killStreak);
            return actionRpg;
        }
    }
}
