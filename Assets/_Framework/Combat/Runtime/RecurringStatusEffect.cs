using System;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Abstract base class for status effects that trigger recurring actions.
    /// Used for Damage Over Time (DOT) and Heal Over Time (HOT) effects.
    /// </summary>
    public abstract class RecurringStatusEffect : StatusEffect
    {
        /// <summary>
        /// Time between each tick in seconds.
        /// </summary>
        public float TickInterval { get; protected set; }

        /// <summary>
        /// Value applied per tick (damage or healing).
        /// </summary>
        public float TickValue { get; protected set; }

        private float _timeSinceLastTick;

        /// <summary>
        /// Create a recurring status effect.
        /// </summary>
        /// <param name="effectId">Unique identifier</param>
        /// <param name="name">Display name</param>
        /// <param name="effectType">Effect classification (usually DOT or HOT)</param>
        /// <param name="duration">Total duration in seconds</param>
        /// <param name="tickInterval">Time between ticks in seconds</param>
        /// <param name="tickValue">Value applied per tick</param>
        /// <param name="stackBehavior">How effect stacks (usually Stack for DOT/HOT)</param>
        /// <param name="maxStacks">Maximum stacks allowed</param>
        protected RecurringStatusEffect(
            string effectId,
            string name,
            StatusEffectType effectType,
            float duration,
            float tickInterval,
            float tickValue,
            StackBehavior stackBehavior = StackBehavior.Stack,
            int maxStacks = 99)
            : base(effectId, name, effectType, duration, stackBehavior, maxStacks)
        {
            if (tickInterval <= 0f)
                throw new ArgumentException("Tick interval must be positive", nameof(tickInterval));

            TickInterval = tickInterval;
            TickValue = tickValue;
            _timeSinceLastTick = 0f;
        }

        /// <summary>
        /// Update recurring effect - triggers ticks at intervals.
        /// </summary>
        public override void OnUpdate(IStatusEffectTarget target, float deltaTime)
        {
            base.OnUpdate(target, deltaTime);

            _timeSinceLastTick += deltaTime;

            // Execute all pending ticks
            while (_timeSinceLastTick >= TickInterval)
            {
                ExecuteTick(target);
                _timeSinceLastTick -= TickInterval;
            }
        }

        /// <summary>
        /// Override this to implement tick behavior (apply damage, healing, etc.).
        /// </summary>
        /// <param name="target">Target to apply tick to</param>
        protected abstract void ExecuteTick(IStatusEffectTarget target);
    }
}
