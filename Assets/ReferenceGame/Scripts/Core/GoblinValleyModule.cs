using System;
using System.Collections.Generic;
using RPGFramework.Bootstrap;
using RPGFramework.Bootstrap.Gameplay;
using RPGFramework.Combat;
using RPGFramework.Core.Composition;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Progression;
using RPGFramework.Quests;

namespace ReferenceGame
{
    /// <summary>
    /// The game layer of Goblin Valley: fills the framework registries with its content and runs
    /// <see cref="GoblinValleyGame"/>.
    /// </summary>
    public sealed class GoblinValleyModule : GameFeatureModule
    {
        public const string ModuleName = "ReferenceGame.GoblinValley";

        private readonly int? _seed;

        public GoblinValleyModule(int? seed = null)
        {
            _seed = seed;
        }

        public override string Name => ModuleName;

        public override IEnumerable<Type> Dependencies => new[]
        {
            typeof(FrameworkCoreModule), typeof(CombatModule), typeof(ProgressionModule), typeof(QuestsModule),
            typeof(ItemsModule), typeof(LootModule)
        };

        /// <summary>
        /// The running game (null before initialization).
        /// </summary>
        public GoblinValleyGame Game { get; private set; }

        protected override void OnConfigure()
        {
            Container.RegisterFactory(c => new GoblinValleyGame(
                c.Resolve<IEntityManager>(),
                c.Resolve<ILifeStateManager>(),
                c.Resolve<IDamageCalculator>(),
                c.Resolve<IProgressionTracker>(),
                c.Resolve<IStatGrowthFormula>(),
                c.Resolve<IQuestManager>(),
                c.Resolve<ILootGenerator>(),
                c.Resolve<IItemRegistry>(),
                c.Resolve<IEventBus>(),
                c.Resolve<ILogger>(),
                _seed));
        }

        protected override void OnInitialize()
        {
            GoblinValleyContent.RegisterItems(Container.Resolve<IItemRegistry>());
            GoblinValleyContent.RegisterLoot(Container.Resolve<ILootTableRegistry>());
            GoblinValleyContent.RegisterQuests(Container.Resolve<IQuestRegistry>());
            GoblinValleyContent.RegisterStatGrowth(Container.Resolve<IStatGrowthFormula>());

            Game = Container.Resolve<GoblinValleyGame>();
            Game.Start();
        }

        protected override void OnShutdown()
        {
            Game?.Dispose();
            Game = null;
        }
    }

    /// <summary>
    /// The module list of Goblin Valley, shared by the scene host and the tests so both run the same game.
    /// </summary>
    public static class GoblinValleyComposition
    {
        public static GoblinValleyModule Configure(IGameCompositionBuilder builder, int? seed = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var game = new GoblinValleyModule(seed);
            builder
                .UseModule(new CombatModule())
                .UseModule(new ProgressionModule())
                .UseModule(new QuestsModule())
                .UseModule(new ItemsModule())
                .UseModule(new LootModule())
                // Kills count for quests by what the victim is ("goblin"); the game knows that
                .UseModule(new CombatQuestsModule(entityId => game.Game?.KindOf(entityId)))
                .UseModule(game);
            return game;
        }
    }
}
