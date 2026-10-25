using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Implementation of IStatusEffectManager.
    /// ECS-based manager for status effects.
    /// </summary>
    public class StatusEffectManager : IStatusEffectManager
    {
        private readonly IEntityManager _entityManager;
        private readonly Dictionary<string, IStatusEffect> _effectRegistry;

        public StatusEffectManager(IEntityManager entityManager)
        {
            _entityManager = entityManager;
            _effectRegistry = new Dictionary<string, IStatusEffect>();
        }

        /// <summary>
        /// Register an effect definition.
        /// </summary>
        public void RegisterEffect(IStatusEffect effect)
        {
            if (!_effectRegistry.ContainsKey(effect.Id))
            {
                _effectRegistry[effect.Id] = effect;
            }
        }

        /// <summary>
        /// Register an effect from data.
        /// </summary>
        public void RegisterEffect(StatusEffectData data)
        {
            var effect = new StatusEffect(data);
            RegisterEffect(effect);
        }

        public bool ApplyEffect(int entityId, string effectId, string sourceId, float currentTime)
        {
            if (!_effectRegistry.TryGetValue(effectId, out var effect))
                return false;

            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
            {
                activeEffects = ActiveEffectsData.CreateEmpty();
            }

            // Check if effect already exists
            var existingEffect = activeEffects.GetEffect(effectId);
            if (existingEffect.HasValue)
            {
                // If stackable, add stack
                if (effect.IsStackable)
                {
                    var instance = new StatusEffectInstance(existingEffect.Value, effect);
                    instance.AddStack();
                    instance.Refresh(effect.Duration);

                    // Update the instance in ActiveEffects
                    activeEffects.RemoveEffect(effectId);
                    activeEffects.AddEffect(instance.ToData());
                    _entityManager.AddComponent(entityId, activeEffects);
                    return true;
                }
                else
                {
                    // Refresh duration if not stackable
                    var instance = new StatusEffectInstance(existingEffect.Value, effect);
                    instance.Refresh(effect.Duration);

                    activeEffects.RemoveEffect(effectId);
                    activeEffects.AddEffect(instance.ToData());
                    _entityManager.AddComponent(entityId, activeEffects);
                    return true;
                }
            }

            // Create new effect instance
            var newInstance = new StatusEffectInstance(effect, entityId, sourceId, currentTime);
            activeEffects.AddEffect(newInstance.ToData());
            _entityManager.AddComponent(entityId, activeEffects);

            return true;
        }

        public bool RemoveEffect(int entityId, string effectId)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return false;

            if (!activeEffects.HasEffect(effectId))
                return false;

            activeEffects.RemoveEffect(effectId);
            _entityManager.AddComponent(entityId, activeEffects);

            return true;
        }

        public void RemoveAllEffects(int entityId)
        {
            var activeEffects = ActiveEffectsData.CreateEmpty();
            _entityManager.AddComponent(entityId, activeEffects);
        }

        public void UpdateEffects(int entityId, float deltaTime)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return;

            if (activeEffects.ActiveInstances == null || activeEffects.ActiveInstances.Count == 0)
                return;

            var instancesToUpdate = new List<StatusEffectInstanceData>(activeEffects.ActiveInstances);

            foreach (var instanceData in instancesToUpdate)
            {
                if (!_effectRegistry.TryGetValue(instanceData.EffectId, out var effect))
                    continue;

                var instance = new StatusEffectInstance(instanceData, effect);
                instance.Update(deltaTime);

                // Update the instance in ActiveEffects
                activeEffects.RemoveEffect(instanceData.EffectId);
                if (!instance.IsExpired)
                {
                    activeEffects.AddEffect(instance.ToData());
                }
            }

            // Remove expired effects
            activeEffects.RemoveExpired();
            _entityManager.AddComponent(entityId, activeEffects);
        }

        public IReadOnlyList<IStatusEffectInstance> GetActiveEffects(int entityId)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return new List<IStatusEffectInstance>();

            if (activeEffects.ActiveInstances == null)
                return new List<IStatusEffectInstance>();

            var instances = new List<IStatusEffectInstance>();
            foreach (var instanceData in activeEffects.ActiveInstances)
            {
                if (_effectRegistry.TryGetValue(instanceData.EffectId, out var effect))
                {
                    instances.Add(new StatusEffectInstance(instanceData, effect));
                }
            }

            return instances;
        }

        public bool HasEffect(int entityId, string effectId)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return false;

            return activeEffects.HasEffect(effectId);
        }

        /// <summary>
        /// Get an effect definition by ID.
        /// </summary>
        public IStatusEffect GetEffect(string effectId)
        {
            return _effectRegistry.TryGetValue(effectId, out var effect) ? effect : null;
        }

        /// <summary>
        /// Process DoT/HoT ticks for an entity.
        /// </summary>
        public void ProcessTicks(int entityId, float currentTime, System.Action<int, string, float, float> onTick = null)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return;

            if (activeEffects.ActiveInstances == null || activeEffects.ActiveInstances.Count == 0)
                return;

            var instancesToUpdate = new List<StatusEffectInstanceData>(activeEffects.ActiveInstances);

            foreach (var instanceData in instancesToUpdate)
            {
                if (!_effectRegistry.TryGetValue(instanceData.EffectId, out var effect))
                    continue;

                var instance = new StatusEffectInstance(instanceData, effect);

                if (instance.ShouldTick(currentTime))
                {
                    instance.UpdateLastTickTime(currentTime);

                    float totalDamage = 0f;
                    float totalHealing = 0f;

                    // Calculate damage/healing per tick
                    foreach (var modifier in effect.Modifiers)
                    {
                        if (modifier.Type == ModifierType.Damage)
                            totalDamage += modifier.Value * instance.CurrentStacks;
                        else if (modifier.Type == ModifierType.Healing)
                            totalHealing += modifier.Value * instance.CurrentStacks;
                    }

                    if (totalDamage > 0f || totalHealing > 0f)
                    {
                        onTick?.Invoke(entityId, effect.Id, totalDamage, totalHealing);
                    }

                    // Update the instance in ActiveEffects
                    activeEffects.RemoveEffect(instanceData.EffectId);
                    activeEffects.AddEffect(instance.ToData());
                }
            }

            _entityManager.AddComponent(entityId, activeEffects);
        }
    }
}
