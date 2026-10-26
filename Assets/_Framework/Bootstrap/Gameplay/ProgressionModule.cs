using System;
using RPGFramework.Events;
using RPGFramework.Progression;

namespace RPGFramework.Bootstrap.Gameplay
{
    public sealed class ProgressionOptions
    {
        /// <summary>
        /// XP curve; a linear 50-level table with 100 XP per level when null.
        /// </summary>
        public ILevelTable LevelTable { get; set; }

        /// <summary>
        /// Timestamp source for progression events (game time); 0 when null.
        /// </summary>
        public Func<float> Clock { get; set; }
    }

    /// <summary>
    /// Registers <see cref="IProgressionTracker"/>, <see cref="IUnlockManager"/> and <see cref="IStatGrowthFormula"/>,
    /// and runs a <see cref="StatGrowthPublisher"/> so every level-up publishes the resulting <see cref="StatIncreasedEvent"/>s.
    /// The game registers its stat growth on the formula when its own module initializes.
    /// </summary>
    public sealed class ProgressionModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.Progression";

        private readonly ProgressionOptions _options;
        private StatGrowthPublisher _statGrowth;

        public ProgressionModule(ProgressionOptions options = null)
        {
            _options = options ?? new ProgressionOptions();
        }

        public override string Name => ModuleName;

        protected override void OnConfigure()
        {
            var levelTable = _options.LevelTable ?? LevelTable.CreateLinear(maxLevel: 50, baseXP: 100);
            Container.RegisterInstance(levelTable);
            Container.RegisterFactory<IStatGrowthFormula>(_ => new StatGrowthFormula());
            Container.RegisterFactory<IProgressionTracker>(c =>
                new ProgressionTracker(c.Resolve<ILevelTable>(), c.Resolve<IEventPublisher>(), _options.Clock));
            Container.RegisterFactory<IUnlockManager>(c => new UnlockManager(c.Resolve<IEventPublisher>(), _options.Clock));
        }

        protected override void OnInitialize()
        {
            _statGrowth = new StatGrowthPublisher(Container.Resolve<IEventBus>(), Container.Resolve<IStatGrowthFormula>(), _options.Clock);
        }

        protected override void OnShutdown()
        {
            _statGrowth?.Dispose();
            _statGrowth = null;
        }
    }
}
