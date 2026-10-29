using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Abilities;
using RPGFramework.Bootstrap;
using RPGFramework.Bootstrap.Gameplay;
using RPGFramework.Combat;
using RPGFramework.Core.Composition;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Interaction;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Progression;
using RPGFramework.Quests;
using RPGFramework.StatusEffects;
using RPGFramework.Systems;
using RPGFramework.Trading;

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
            typeof(ItemsModule), typeof(LootModule), typeof(TradingModule), typeof(InteractionModule)
        };

        /// <summary>
        /// The running world (null before initialization and after shutdown).
        /// </summary>
        public ActorWorld World { get; private set; }

        private ActorAchievements _achievementFeed;

        /// <summary>
        /// The PvP match (null before initialization; idle when <see cref="ActionRpgSettings.Pvp"/> is not set).
        /// </summary>
        public PvpMatch Pvp { get; private set; }

        /// <summary>
        /// Distance between two characters for ability range (the presentation knows where they are); null counts
        /// every target as in range.
        /// </summary>
        public Func<int, int, float> Distance { get; set; }

        /// <summary>
        /// Distance from a player to a place, given to <see cref="IInteractionService.Distance"/> when the world
        /// starts; null leaves the service's own (every place in reach, unless the game sets one).
        /// </summary>
        public Func<int, InteractionPoint, float> InteractionDistance { get; set; }

        protected override void OnConfigure()
        {
            Container.RegisterInstance(_settings);
            Container.RegisterInstance(_content);
            Container.RegisterFactory(c => new ActorWorld(
                _settings,
                c.Resolve<IEntityManager>(),
                c.Resolve<ILifeStateManager>(),
                _settings.DamageCalculator ?? c.Resolve<IDamageCalculator>(),
                c.Resolve<IProgressionTracker>(),
                c.Resolve<IQuestManager>(),
                c.Resolve<IQuestRegistry>(),
                c.Resolve<ILootGenerator>(),
                c.Resolve<IItemRegistry>(),
                c.Resolve<IEventBus>(),
                c.Resolve<ILogger>(),
                c.Resolve<ICurrencyWallet>()));
            AddSystem<WaveSystem>();
            Container.RegisterFactory(c => c.Resolve<ActorWorld>().Crafting);
            AddSystem<ActorCrafting>();
            AddSystem<PvpMatch>();
            if (_settings.UsesStatusEffects(_content))
            {
                Container.RegisterFactory(c => new ActorEffects(c.Resolve<ActorWorld>(), c.Resolve<IStatusEffectManager>(),
                    c.Resolve<IEventBus>()));
                AddSystem<ActorEffects>();
            }
            if (_settings.UsesAbilities(_content))
            {
                Container.RegisterFactory(c => new ActorAbilities(c.Resolve<ActorWorld>(), c.Resolve<IAbilityExecutor>(),
                    c.Resolve<ICooldownTracker>(), c.Resolve<IAbilityRegistry>(), c.Resolve<IEventBus>()));
                AddSystem<ActorAbilities>();
            }
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
            World.Content = _content;
            World.Trading = Container.Resolve<ITradingManager>();
            World.Interactions = Container.Resolve<IInteractionService>();
            World.GamePause = Container.Resolve<IGamePause>();
            foreach (var gear in _content.Gear.Where(g => g != null && !string.IsNullOrEmpty(g.ItemId)))
                World.Gear.Register(gear);
            foreach (var recipe in _content.Recipes)
                World.Crafting.Register(recipe);
            if (_content.Achievements.Count > 0)
            {
                var achievements = new RPGFramework.Achievements.AchievementRegistry();
                foreach (var achievement in _content.Achievements)
                {
                    if (achievement != null)
                        achievements.RegisterAchievement(achievement);
                }
                var bus = Container.Resolve<IEventBus>();
                World.Achievements = new RPGFramework.Achievements.AchievementTracker(achievements, Container.Resolve<IEntityManager>(), bus);
                _achievementFeed = new ActorAchievements(World, World.Achievements, bus);
            }
            if (Container.IsRegistered<ITradePricing>())
                World.TradePricing = Container.Resolve<ITradePricing>();
            if (InteractionDistance != null)
                World.Interactions.Distance = InteractionDistance;
            Pvp = Container.Resolve<PvpMatch>();
            if (_settings.UsesStatusEffects(_content))
            {
                var effects = Container.Resolve<IStatusEffectManager>();
                foreach (var effect in _content.StatusEffects.Where(e => e != null))
                    effects.RegisterEffect(effect);
                World.Effects = Container.Resolve<ActorEffects>();
            }
            if (_settings.UsesAbilities(_content))
            {
                var abilities = Container.Resolve<IAbilityRegistry>();
                foreach (var ability in _content.Abilities.Where(a => a != null))
                    abilities.RegisterAbility(ability);
                World.Abilities = Container.Resolve<ActorAbilities>();
            }
            World.Start();
        }

        protected override void OnShutdown()
        {
            _achievementFeed?.Dispose();
            _achievementFeed = null;
            World?.Dispose();
            World = null;
            Pvp = null;
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
            if (settings != null && settings.UsesStatusEffects(content))
            {
                builder.UseModule(new StatusEffectsModule(new StatusEffectsOptions
                {
                    Target = new ActorEffectTarget(() => game.World)
                }));
            }
            if (settings != null && settings.UsesAbilities(content))
            {
                builder.UseModule(new AbilitiesModule(new AbilitiesOptions
                {
                    // Positions live in the presentation: a game that checks range sets ActionRpgModule.Distance
                    DistanceCheck = (a, b) => game.Distance?.Invoke(a, b) ?? 0f,
                    RelationshipCheck = (a, b) => game.World?.AreHostile(a, b) ?? false
                }));
            }
            builder
                .UseModule(new CombatModule())
                .UseModule(new ProgressionModule())
                .UseModule(new QuestsModule())
                .UseModule(new ItemsModule())
                .UseModule(new LootModule())
                // Shops pay from the world's wallet and move items in and out of the backpacks
                .UseModule(new TradingModule(new TradingOptions
                {
                    Inventory = new ActorTradeInventory(() => game.World),
                    // Prices are the buyer's (World.TradePricing), even when the party's wallet pays
                    Pricing = new ActorTradePricing(() => game.World)
                }))
                // Chests, doors, levers: the host measures how near the players are
                .UseModule(new InteractionModule())
                // Kills count for quests by character id ("goblin"), and for the party whoever in it made them
                .UseModule(new CombatQuestsModule(entityId => game.World?.QuestTargetOf(entityId),
                    entityId => game.World?.CreditOf(entityId) ?? entityId))
                .UseModule(game);
            return game;
        }
    }
}
