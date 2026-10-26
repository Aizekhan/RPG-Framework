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

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Registers the game's content into the framework registries and runs the <see cref="ActorWorld"/> with its
    /// waves. Use <see cref="ActionRpgComposition"/> to get it together with the modules it needs.
    /// </summary>
    public sealed class ActionRpgModule : GameFeatureModule
    {
        public const string ModuleName = "RPGFramework.ActionRpg";

        private readonly ActionRpgSettings _settings;
        private readonly ActionRpgContent _content;

        public ActionRpgModule(ActionRpgSettings settings, ActionRpgContent content = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _content = content ?? new ActionRpgContent();
        }

        public override string Name => ModuleName;

        public override IEnumerable<Type> Dependencies => new[]
        {
            typeof(FrameworkCoreModule), typeof(CombatModule), typeof(ProgressionModule), typeof(QuestsModule),
            typeof(ItemsModule), typeof(LootModule)
        };

        /// <summary>
        /// The running world (null before initialization and after shutdown).
        /// </summary>
        public ActorWorld World { get; private set; }

        protected override void OnConfigure()
        {
            Container.RegisterInstance(_settings);
            Container.RegisterInstance(_content);
            Container.RegisterFactory(c => new ActorWorld(
                _settings,
                c.Resolve<IEntityManager>(),
                c.Resolve<ILifeStateManager>(),
                c.Resolve<IDamageCalculator>(),
                c.Resolve<IProgressionTracker>(),
                c.Resolve<IQuestManager>(),
                c.Resolve<IQuestRegistry>(),
                c.Resolve<ILootGenerator>(),
                c.Resolve<IItemRegistry>(),
                c.Resolve<IEventBus>(),
                c.Resolve<ILogger>()));
            AddSystem<WaveSystem>();
        }

        protected override void OnInitialize()
        {
            var items = Container.Resolve<IItemRegistry>();
            foreach (var item in _content.Items)
                items.RegisterItem(item);
            var tables = Container.Resolve<ILootTableRegistry>();
            foreach (var table in _content.LootTables)
                tables.RegisterTable(table);
            var quests = Container.Resolve<IQuestRegistry>();
            foreach (var quest in _content.Quests)
                quests.RegisterQuest(quest);

            // The player's growth drives StatIncreasedEvent on level-up; the world applies it to the player
            var player = _settings.Player;
            if (player != null)
            {
                var growth = Container.Resolve<IStatGrowthFormula>();
                growth.RegisterStatGrowth(new StatGrowthData(nameof(ActorStat.MaxHealth), player.MaxHealth, player.HealthPerLevel));
                growth.RegisterStatGrowth(new StatGrowthData(nameof(ActorStat.Attack), player.Attack, player.AttackPerLevel));
                growth.RegisterStatGrowth(new StatGrowthData(nameof(ActorStat.Defense), player.Defense, player.DefensePerLevel));
            }

            World = Container.Resolve<ActorWorld>();
            World.Start();
        }

        protected override void OnShutdown()
        {
            World?.Dispose();
            World = null;
        }
    }

    /// <summary>
    /// The module list of an action RPG, shared by the scene host and tests so both run the same game.
    /// </summary>
    public static class ActionRpgComposition
    {
        public static ActionRpgModule Configure(IGameCompositionBuilder builder, ActionRpgSettings settings,
            ActionRpgContent content = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var game = new ActionRpgModule(settings, content);
            builder
                .UseModule(new CombatModule())
                .UseModule(new ProgressionModule())
                .UseModule(new QuestsModule())
                .UseModule(new ItemsModule())
                .UseModule(new LootModule())
                // Kills count for quests by character id ("goblin")
                .UseModule(new CombatQuestsModule(entityId => game.World?.QuestTargetOf(entityId)))
                .UseModule(game);
            return game;
        }
    }
}
