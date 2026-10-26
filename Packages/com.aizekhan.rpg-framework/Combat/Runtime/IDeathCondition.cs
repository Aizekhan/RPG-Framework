namespace RPGFramework.Combat
{
    /// <summary>
    /// Represents a condition that determines whether an entity should die.
    /// </summary>
    /// <remarks>
    /// Death conditions provide extensible logic for when an entity transitions to death state.
    /// Examples include resource depletion (HP ≤ 0), instant death effects, timeout conditions, etc.
    /// </remarks>
    public interface IDeathCondition
    {
        /// <summary>
        /// Evaluates whether the target entity should die based on this condition.
        /// </summary>
        /// <param name="target">The entity to evaluate.</param>
        /// <returns>True if the entity should die; otherwise, false.</returns>
        bool ShouldDie(ILifeStateHandler target);

        /// <summary>
        /// Gets the reason for death if this condition is met.
        /// </summary>
        /// <returns>A string describing the death reason (e.g., "resource_depletion", "instant_death").</returns>
        string GetDeathReason();

        /// <summary>
        /// Gets the priority of this death condition.
        /// </summary>
        /// <remarks>
        /// Higher priority conditions are evaluated first.
        /// Useful when multiple death conditions exist simultaneously.
        /// </remarks>
        int Priority { get; }
    }
}
