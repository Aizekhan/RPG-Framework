using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Default implementation of IStatusEffectManager.
    /// Manages status effects on multiple targets, handling application, removal, and updates.
    /// </summary>
    public class StatusEffectManager : IStatusEffectManager
    {
        // Track effects per target
        private readonly Dictionary<string, List<IStatusEffect>> _targetEffects;

        public StatusEffectManager()
        {
            _targetEffects = new Dictionary<string, List<IStatusEffect>>();
        }

        /// <summary>
        /// Apply status effect to target, handling stacking behavior.
        /// </summary>
        public bool ApplyEffect(IStatusEffectTarget target, IStatusEffect effect)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            // Check if target can receive effect
            if (!target.CanReceiveEffect(effect))
                return false;

            // Get or create effect list for target
            if (!_targetEffects.TryGetValue(target.TargetId, out var effects))
            {
                effects = new List<IStatusEffect>();
                _targetEffects[target.TargetId] = effects;
            }

            // Check for existing effect with same ID
            var existing = effects.FirstOrDefault(e => e.EffectId == effect.EffectId && e.IsActive);

            if (existing != null)
            {
                return HandleExistingEffect(target, existing, effect, effects);
            }

            // New effect - add and apply
            effects.Add(effect);
            effect.OnApplied(target);
            return true;
        }

        /// <summary>
        /// Handle applying effect when same effect already exists on target.
        /// Implements stacking behavior.
        /// </summary>
        private bool HandleExistingEffect(
            IStatusEffectTarget target,
            IStatusEffect existing,
            IStatusEffect newEffect,
            List<IStatusEffect> effects)
        {
            switch (existing.StackBehavior)
            {
                case StackBehavior.Stack:
                    // Allow multiple instances
                    effects.Add(newEffect);
                    newEffect.OnApplied(target);
                    return true;

                case StackBehavior.Replace:
                    // Remove old, add new
                    existing.OnRemoved(target);
                    effects.Remove(existing);
                    effects.Add(newEffect);
                    newEffect.OnApplied(target);
                    return true;

                case StackBehavior.Refresh:
                    // Keep existing, refresh duration
                    existing.RefreshDuration();
                    return true;

                case StackBehavior.Ignore:
                    // Keep existing, ignore new
                    return false;

                case StackBehavior.StackCount:
                    // Increment stack count
                    bool added = existing.AddStack();
                    if (added)
                    {
                        // Refresh duration when stacking
                        existing.RefreshDuration();
                    }
                    return added;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Remove specific effect from target.
        /// </summary>
        public bool RemoveEffect(IStatusEffectTarget target, string effectId)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(effectId))
                throw new ArgumentNullException(nameof(effectId));

            if (!_targetEffects.TryGetValue(target.TargetId, out var effects))
                return false;

            var effect = effects.FirstOrDefault(e => e.EffectId == effectId && e.IsActive);
            if (effect == null)
                return false;

            effect.OnRemoved(target);
            effects.Remove(effect);
            return true;
        }

        /// <summary>
        /// Remove all effects from target.
        /// </summary>
        public void RemoveAllEffects(IStatusEffectTarget target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (!_targetEffects.TryGetValue(target.TargetId, out var effects))
                return;

            // Call OnRemoved for all effects
            foreach (var effect in effects.ToList())
            {
                if (effect.IsActive)
                {
                    effect.OnRemoved(target);
                }
            }

            effects.Clear();
        }

        /// <summary>
        /// Remove all effects of specific type from target.
        /// </summary>
        public void RemoveEffectsOfType(IStatusEffectTarget target, StatusEffectType effectType)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (!_targetEffects.TryGetValue(target.TargetId, out var effects))
                return;

            var toRemove = effects.Where(e => e.EffectType == effectType && e.IsActive).ToList();

            foreach (var effect in toRemove)
            {
                effect.OnRemoved(target);
                effects.Remove(effect);
            }
        }

        /// <summary>
        /// Get all active effects on target.
        /// </summary>
        public IReadOnlyList<IStatusEffect> GetActiveEffects(IStatusEffectTarget target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (!_targetEffects.TryGetValue(target.TargetId, out var effects))
                return Array.Empty<IStatusEffect>();

            return effects.Where(e => e.IsActive).ToList();
        }

        /// <summary>
        /// Check if target has specific effect active.
        /// </summary>
        public bool HasEffect(IStatusEffectTarget target, string effectId)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(effectId))
                throw new ArgumentNullException(nameof(effectId));

            if (!_targetEffects.TryGetValue(target.TargetId, out var effects))
                return false;

            return effects.Any(e => e.EffectId == effectId && e.IsActive);
        }

        /// <summary>
        /// Update all effects on target.
        /// Updates durations, triggers recurring effects, removes expired effects.
        /// </summary>
        public void UpdateEffects(IStatusEffectTarget target, float deltaTime)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (!_targetEffects.TryGetValue(target.TargetId, out var effects))
                return;

            // Update each effect (use ToList to allow modification during iteration)
            foreach (var effect in effects.ToList())
            {
                if (!effect.IsActive)
                    continue;

                // Update effect logic (triggers recurring effects)
                effect.OnUpdate(target, deltaTime);

                // Update duration (check if expired)
                bool expired = effect.UpdateDuration(deltaTime);

                if (expired)
                {
                    effect.OnRemoved(target);
                    effects.Remove(effect);
                }
            }
        }
    }
}
