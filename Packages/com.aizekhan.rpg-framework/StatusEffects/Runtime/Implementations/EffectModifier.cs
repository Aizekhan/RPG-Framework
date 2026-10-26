namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Implementation of IEffectModifier.
    /// </summary>
    public class EffectModifier : IEffectModifier
    {
        public ModifierType Type { get; }
        public string TargetId { get; }
        public float Value { get; }
        public ModifierOperation Operation { get; }

        public EffectModifier(ModifierType type, string targetId, float value, ModifierOperation operation)
        {
            Type = type;
            TargetId = targetId;
            Value = value;
            Operation = operation;
        }

        public EffectModifier(EffectModifierData data)
        {
            Type = data.Type;
            TargetId = data.TargetId;
            Value = data.Value;
            Operation = data.Operation;
        }

        public float Apply(float currentValue)
        {
            switch (Operation)
            {
                case ModifierOperation.Add:
                    return currentValue + Value;
                case ModifierOperation.Multiply:
                    return currentValue * Value;
                case ModifierOperation.Override:
                    return Value;
                default:
                    return currentValue;
            }
        }
    }
}
