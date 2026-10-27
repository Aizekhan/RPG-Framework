namespace RPGFramework.Crafting
{
    /// <summary>
    /// Default implementation of ICraftingRequirement.
    /// </summary>
    public class CraftingRequirement : ICraftingRequirement
    {
        /// <inheritdoc/>
        public RequirementType Type { get; }

        /// <inheritdoc/>
        public string ParameterId { get; }

        /// <inheritdoc/>
        public int Value { get; }

        /// <summary>
        /// Create a new crafting requirement.
        /// </summary>
        public CraftingRequirement(RequirementType type, string parameterId, int value)
        {
            if (string.IsNullOrEmpty(parameterId))
                throw new System.ArgumentException("Parameter ID cannot be null or empty", nameof(parameterId));
            if (value < 0)
                throw new System.ArgumentException("Value cannot be negative", nameof(value));

            Type = type;
            ParameterId = parameterId;
            Value = value;
        }

        /// <summary>
        /// A plain requirement knows nothing about the crafter, so it raises no objection of its own. The crafting
        /// manager checks it for real (<see cref="CraftingManager.IsRequirementMet"/>); a game may also write its own
        /// <see cref="ICraftingRequirement"/> that checks itself here.
        /// </summary>
        public bool IsSatisfied(int entityId)
        {
            return true;
        }

        /// <summary>
        /// Create from serializable data.
        /// </summary>
        public static CraftingRequirement FromData(CraftingRequirementData data)
        {
            return new CraftingRequirement(data.Type, data.ParameterId, data.Value);
        }
    }
}
