namespace RPGFramework.Crafting
{
    /// <summary>
    /// Interface for crafting requirements.
    /// Represents a requirement that must be met to craft a recipe.
    /// </summary>
    public interface ICraftingRequirement
    {
        /// <summary>
        /// Type of requirement.
        /// </summary>
        RequirementType Type { get; }

        /// <summary>
        /// Parameter ID (skill ID, tool ID, quest ID, etc.).
        /// </summary>
        string ParameterId { get; }

        /// <summary>
        /// Numeric value (skill level, quantity, etc.).
        /// </summary>
        int Value { get; }

        /// <summary>
        /// Validate that this requirement is met.
        /// </summary>
        /// <param name="entityId">Entity ID to validate against</param>
        /// <returns>True if requirement is met</returns>
        bool IsSatisfied(int entityId);
    }
}
