using System;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Data structure for effect modifiers.
    /// </summary>
    [Serializable]
    public struct EffectModifierData
    {
        public ModifierType Type;
        public string TargetId;
        public float Value;
        public ModifierOperation Operation;

        /// <summary>
        /// Create a new EffectModifierData with default Add operation.
        /// </summary>
        public static EffectModifierData Create(ModifierType type, string targetId, float value)
        {
            return new EffectModifierData
            {
                Type = type,
                TargetId = targetId,
                Value = value,
                Operation = ModifierOperation.Add
            };
        }

        /// <summary>
        /// Create a new EffectModifierData with specified operation.
        /// </summary>
        public static EffectModifierData Create(ModifierType type, string targetId, float value, ModifierOperation operation)
        {
            return new EffectModifierData
            {
                Type = type,
                TargetId = targetId,
                Value = value,
                Operation = operation
            };
        }
    }
}
