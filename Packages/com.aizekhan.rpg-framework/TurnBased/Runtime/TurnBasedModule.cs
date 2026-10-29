using System;
using System.Collections.Generic;
using RPGFramework.Abilities;
using RPGFramework.Bootstrap;
using RPGFramework.Bootstrap.Gameplay;
using RPGFramework.Core.Composition;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Progression;
using RPGFramework.Quests;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// Registers the game's content into the framework registries (items, loot tables, abilities; status effects go
    /// to the world's status effect manager) and starts the <see cref="TurnBasedWorld"/>. Also registers the ability
    /// registry and cooldown tracker when no other module did. Use <see cref="TurnBasedComposition"/> to get it
    /// together with the modules it needs.
    /// </summary>
    public sealed class TurnBasedModule : GameFeatureModule
    {
        public const string ModuleName = "RPGFramework.TurnBased";

        private readonly TurnBasedSettings _settings;
        private readonly TurnBasedContent _content;

        public TurnBasedModule(TurnBasedSettings settings, TurnBasedContent content = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _content = content ?? new TurnBasedContent();
        }

        public override string Name => ModuleName;

        public override IEnumerable<Type> Dependencies => new[]
        {
            typeof(FrameworkCoreModule), typeof(ProgressionModule), typeof(ItemsModule), typeof(LootModule)
        };

        /// <summary>
        /// The running world (null before initialization and after shutdown).
        /// </summary>
        public TurnBasedWorld World { get; private set; }

        protected override void OnConfigure()
        {
            Container.RegisterInstance(_settings);
            Container.RegisterInstance(_content);
            if (!Container.IsRegistered<IAbilityRegistry>())
                Container.RegisterFactory<IAbilityRegistry>(_ => new AbilityRegistry());
            if (!Container.IsRegistered<ICooldownTracker>())
                Container.RegisterFactory<ICooldownTracker>(c => new CooldownTracker(c.Resolve<IEventPublisher>()));
            Container.RegisterFactory(c => new TurnBasedWorld(
                _settings,
                _content,
                c.Resolve<IEntityManager>(),
                c.Resolve<IProgressionTracker>(),
                c.Resolve<ILootGenerator>(),
                c.Resolve<IItemRegistry>(),
                c.Resolve<IAbilityRegistry>(),
                c.Resolve<ICooldownTracker>(),
                c.Resolve<IEventBus>(),
                c.Resolve<ILogger>(),
                c.Resolve<IStatGrowthFormula>(),
                // Quests are there when the game composes QuestsModule (TurnBasedComposition does)
                Container.IsRegistered<IQuestManager>() ? c.Resolve<IQuestManager>() : null,
                Container.IsRegistered<IQuestRegistry>() ? c.Resolve<IQuestRegistry>() : null));
        }

        protected override void OnInitialize()
        {
            var items = Container.Resolve<IItemRegistry>();
            foreach (var item in _content.Items)
            {
                if (item != null)
                    items.RegisterItem(item);
            }
            var tables = Container.Resolve<ILootTableRegistry>();
            foreach (var table in _content.LootTables)
            {
                if (table != null)
                    tables.RegisterTable(table);
            }
            var abilities = Container.Resolve<IAbilityRegistry>();
            foreach (var ability in _content.Abilities)
                abilities.RegisterAbility(ability);
            if (Container.IsRegistered<IQuestRegistry>())
            {
                var quests = Container.Resolve<IQuestRegistry>();
                foreach (var quest in _content.Quests)
                {
                    if (quest != null)
                        quests.RegisterQuest(quest);
                }
            }
            else if (_content.Quests.Count > 0)
            {
                Container.Resolve<ILogger>()?.LogWarning("The setup has quests but the game does not compose QuestsModule; " +
                                                         "they are left out", "TurnBased");
            }

            World = Container.Resolve<TurnBasedWorld>();
            World.Start();
        }

        protected override void OnShutdown()
        {
            World?.Dispose();
            World = null;
        }
    }

    /// <summary>
    /// The module list of a turn-based RPG, shared by the scene host and tests so both run the same game.
    /// </summary>
    public static class TurnBasedComposition
    {
        public static TurnBasedModule Configure(IGameCompositionBuilder builder, TurnBasedSettings settings,
            TurnBasedContent content = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var game = new TurnBasedModule(settings, content);
            builder
                .UseModule(new ProgressionModule(new ProgressionOptions { LevelTable = settings.LevelTable }))
                .UseModule(new ItemsModule())
                .UseModule(new LootModule())
                .UseModule(new QuestsModule())
                .UseModule(game);
            return game;
        }
    }
}
