using System;
using RPGFramework.ECS;
using RPGFramework.Stats;
using RPGFramework.StatusEffects;

namespace RPGFramework.Integration.StatusEffectsStats
{
    /// <summary>
    /// Lands status effects on the entity's <see cref="StatContainer{TStatType, TValue}"/> component (float values).
    /// Pass it to <see cref="StatusEffectManager"/> as the target.
    /// Stat modifiers become stat container modifiers, matched by name to <typeparamref name="TStatType"/>:
    /// Add is additive, Multiply x1.5 is multiplicative +0.5 (the container multiplies by 1 + value), Override overrides.
    /// A resource is the base value of the stat with that name, so Mana drain lowers the base Mana.
    /// Ids that name no stat, and entities without the component, are ignored. Movement and controls are not stats:
    /// read them from the manager (GetMovementMultiplier, IsControlled).
    /// </summary>
    public sealed class StatContainerEffectTarget<TStatType> : IStatusEffectTarget
        where TStatType : struct, Enum, IConvertible
    {
        private readonly IEntityManager _entities;

        public StatContainerEffectTarget(IEntityManager entities)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        public void ApplyStatModifier(int entityId, string modifierId, string statId, ModifierOperation operation, float value)
        {
            if (!TryParseStat(statId, out var stat) || !HasStats(entityId))
                return;

            StatModifierType type;
            switch (operation)
            {
                case ModifierOperation.Multiply:
                    type = StatModifierType.Multiplicative;
                    value -= 1f;
                    break;
                case ModifierOperation.Override:
                    type = StatModifierType.Override;
                    break;
                default:
                    type = StatModifierType.Additive;
                    break;
            }

            ref var stats = ref _entities.GetComponentRef<StatContainer<TStatType, float>>(entityId);
            stats.AddModifier(new StatModifier<TStatType, float>(modifierId, stat, value, type));
        }

        public void RemoveStatModifier(int entityId, string modifierId)
        {
            if (!HasStats(entityId))
                return;

            ref var stats = ref _entities.GetComponentRef<StatContainer<TStatType, float>>(entityId);
            stats.RemoveModifier(modifierId);
        }

        public bool TryGetResource(int entityId, string resourceId, out float value)
        {
            value = 0f;
            if (!TryParseStat(resourceId, out var stat) || !HasStats(entityId))
                return false;

            ref var stats = ref _entities.GetComponentRef<StatContainer<TStatType, float>>(entityId);
            if (!stats.HasStat(stat))
                return false;

            value = stats.GetBaseStat(stat);
            return true;
        }

        public void ChangeResource(int entityId, string resourceId, float amount)
        {
            if (!TryParseStat(resourceId, out var stat) || !HasStats(entityId))
                return;

            ref var stats = ref _entities.GetComponentRef<StatContainer<TStatType, float>>(entityId);
            if (stats.HasStat(stat))
                stats.SetBaseStat(stat, stats.GetBaseStat(stat) + amount);
        }

        public void SetMovementMultiplier(int entityId, float multiplier)
        {
            // Not a stat; the game reads StatusEffectManager.GetMovementMultiplier
        }

        public void SetControl(int entityId, string controlId, bool active)
        {
            // Not a stat; the game reads StatusEffectManager.IsControlled
        }

        private bool HasStats(int entityId)
        {
            return _entities.HasComponent<StatContainer<TStatType, float>>(entityId);
        }

        private static bool TryParseStat(string statId, out TStatType stat)
        {
            stat = default;
            return !string.IsNullOrEmpty(statId)
                   && Enum.TryParse(statId, false, out stat)
                   && Enum.IsDefined(typeof(TStatType), stat);
        }
    }
}
