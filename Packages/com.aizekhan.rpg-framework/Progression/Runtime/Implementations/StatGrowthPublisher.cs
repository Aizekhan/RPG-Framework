using System;
using RPGFramework.Events;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Turns level-ups into stat growth: on every <see cref="LevelUpEvent"/> it publishes one
    /// <see cref="StatIncreasedEvent"/> per stat of the entity's growth in the <see cref="IStatGrowthFormula"/> (its
    /// profile, or the shared growth) whose value changed.
    /// The game applies the new values to its own stat storage by subscribing to <see cref="StatIncreasedEvent"/>.
    /// Dispose to stop listening.
    /// </summary>
    public sealed class StatGrowthPublisher : IDisposable
    {
        private readonly IEventBus _bus;
        private readonly IStatGrowthFormula _formula;
        private readonly Func<float> _clock;
        private readonly IDisposable _subscription;

        public StatGrowthPublisher(IEventBus bus, IStatGrowthFormula formula, Func<float> clock = null)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _formula = formula ?? throw new ArgumentNullException(nameof(formula));
            _clock = clock;
            _subscription = _bus.Subscribe<LevelUpEvent>(OnLevelUp);
        }

        /// <summary>
        /// Stat events published since creation.
        /// </summary>
        public int PublishedCount { get; private set; }

        public void Dispose() => _subscription.Dispose();

        private void OnLevelUp(LevelUpEvent levelUp)
        {
            if (levelUp.NewLevel <= levelUp.OldLevel)
                return;

            float now = _clock?.Invoke() ?? levelUp.Timestamp;
            // The keys are every stat the entity grows; the values are only the new ones, so compute the old ones too
            int entityId = levelUp.EntityId;
            foreach (var pair in _formula.GetStatChangesForLevelUp(entityId, levelUp.OldLevel, levelUp.NewLevel))
            {
                float oldValue = _formula.CalculateStatAtLevel(entityId, pair.Key, levelUp.OldLevel);
                if (pair.Value.Equals(oldValue))
                    continue;

                PublishedCount++;
                _bus.Publish(new StatIncreasedEvent
                {
                    EntityId = entityId,
                    StatId = pair.Key,
                    OldValue = oldValue,
                    NewValue = pair.Value,
                    Level = levelUp.NewLevel,
                    Timestamp = now
                });
            }
        }
    }
}
