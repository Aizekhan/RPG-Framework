using System;

namespace RPGFramework.Crafting
{
    /// <summary>
    /// Serializable data for a crafting requirement.
    /// </summary>
    [Serializable]
    public struct CraftingRequirementData
    {
        /// <summary>
        /// Type of requirement.
        /// </summary>
        public RequirementType Type;

        /// <summary>
        /// Parameter ID (skill ID, tool ID, quest ID, etc. depending on type).
        /// </summary>
        public string ParameterId;

        /// <summary>
        /// Numeric value (skill level, quantity, etc.).
        /// </summary>
        public int Value;

        /// <summary>
        /// Create a new crafting requirement data.
        /// </summary>
        public CraftingRequirementData(RequirementType type, string parameterId, int value)
        {
            if (string.IsNullOrEmpty(parameterId))
                throw new ArgumentException("Parameter ID cannot be null or empty", nameof(parameterId));
            if (value < 0)
                throw new ArgumentException("Value cannot be negative", nameof(value));

            Type = type;
            ParameterId = parameterId;
            Value = value;
        }
    }
}
