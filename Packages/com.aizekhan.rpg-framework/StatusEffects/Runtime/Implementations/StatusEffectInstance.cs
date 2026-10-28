namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Implementation of IStatusEffectInstance.
    /// </summary>
    public class StatusEffectInstance : IStatusEffectInstance
    {
        public string EffectId { get; }
        public int EntityId { get; }
        public float StartTime { get; }
        public float RemainingDuration { get; private set; }
        public int CurrentStacks { get; private set; }
        public string SourceId { get; }
        public float LastTickTime { get; private set; }
        public float Power { get; private set; } = 1f;
        public bool IsExpired => RemainingDuration <= 0f && RemainingDuration != -1f;

        private readonly IStatusEffect _effect;

        public StatusEffectInstance(IStatusEffect effect, int entityId, string sourceId, float currentTime)
        {
            _effect = effect;
            EffectId = effect.Id;
            EntityId = entityId;
            SourceId = sourceId;
            StartTime = currentTime;
            RemainingDuration = effect.Duration;
            CurrentStacks = 1;
            LastTickTime = currentTime;
        }

        public StatusEffectInstance(StatusEffectInstanceData data, IStatusEffect effect)
        {
            _effect = effect;
            EffectId = data.EffectId;
            EntityId = data.EntityId;
            SourceId = data.SourceId;
            StartTime = data.StartTime;
            RemainingDuration = data.RemainingDuration;
            CurrentStacks = data.CurrentStacks;
            LastTickTime = data.LastTickTime;
            Power = data.Power > 0f ? data.Power : 1f;
        }

        public void Update(float deltaTime)
        {
            if (RemainingDuration == -1f) // Permanent effect
                return;

            RemainingDuration -= deltaTime;

            if (RemainingDuration < 0f)
                RemainingDuration = 0f;
        }

        public void AddStack()
        {
            if (!_effect.IsStackable)
                return;

            if (CurrentStacks < _effect.MaxStacks)
                CurrentStacks++;
        }

        public void Refresh(float newDuration)
        {
            RemainingDuration = newDuration;
        }

        /// <summary>
        /// Sets how strong it is (above 0; anything else counts as 1).
        /// </summary>
        public void SetPower(float power)
        {
            Power = power > 0f && !float.IsInfinity(power) ? power : 1f;
        }

        public bool ShouldTick(float currentTime)
        {
            if (_effect.TickInterval <= 0f)
                return false;

            return (currentTime - LastTickTime) >= _effect.TickInterval;
        }

        public void UpdateLastTickTime(float currentTime)
        {
            LastTickTime = currentTime;
        }

        public StatusEffectInstanceData ToData()
        {
            return new StatusEffectInstanceData
            {
                EffectId = EffectId,
                EntityId = EntityId,
                StartTime = StartTime,
                RemainingDuration = RemainingDuration,
                CurrentStacks = CurrentStacks,
                SourceId = SourceId,
                LastTickTime = LastTickTime,
                Power = Power
            };
        }
    }
}
