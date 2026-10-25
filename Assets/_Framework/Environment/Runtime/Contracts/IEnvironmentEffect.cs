namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Applies environmental effects to gameplay mechanics.
    /// </summary>
    public interface IEnvironmentEffect
    {
        /// <summary>
        /// Calculates the visibility modifier based on current environmental conditions.
        /// </summary>
        /// <param name="baseVisibility">The base visibility range.</param>
        /// <returns>Modified visibility value.</returns>
        float CalculateVisibilityModifier(float baseVisibility);

        /// <summary>
        /// Calculates the movement speed modifier based on current environmental conditions.
        /// </summary>
        /// <param name="baseSpeed">The base movement speed.</param>
        /// <returns>Modified movement speed value.</returns>
        float CalculateMovementModifier(float baseSpeed);

        /// <summary>
        /// Calculates the combat accuracy modifier based on current environmental conditions.
        /// </summary>
        /// <param name="baseAccuracy">The base combat accuracy.</param>
        /// <returns>Modified accuracy value.</returns>
        float CalculateCombatModifier(float baseAccuracy);

        /// <summary>
        /// Calculates the stamina drain rate modifier based on current environmental conditions.
        /// </summary>
        /// <param name="baseDrain">The base stamina drain rate.</param>
        /// <returns>Modified stamina drain value.</returns>
        float CalculateStaminaModifier(float baseDrain);

        /// <summary>
        /// Gets whether the current environment is considered harsh (deals damage over time).
        /// </summary>
        bool IsHarshEnvironment();

        /// <summary>
        /// Gets the environmental damage per second in harsh conditions.
        /// </summary>
        float GetEnvironmentalDamage();

        /// <summary>
        /// Updates the effect calculations based on current environment state.
        /// </summary>
        void UpdateEffects(EnvironmentData environmentData);
    }
}
