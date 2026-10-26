namespace RPGFramework.Factions
{
    /// <summary>
    /// Represents an entity's reputation with a specific faction.
    /// Tracks reputation value and calculates standing based on thresholds.
    /// </summary>
    public interface IFactionReputation
    {
        /// <summary>
        /// ID of the faction this reputation is for.
        /// </summary>
        string FactionId { get; }

        /// <summary>
        /// Current reputation value (-100 to +100).
        /// Higher values indicate better standing.
        /// </summary>
        int Reputation { get; }

        /// <summary>
        /// Current standing based on reputation value.
        /// Automatically calculated from Reputation using thresholds.
        /// </summary>
        FactionStanding Standing { get; }

        /// <summary>
        /// Change the reputation value by a delta amount.
        /// Automatically clamps to [-100, +100] range.
        /// </summary>
        /// <param name="delta">Amount to add (positive) or subtract (negative).</param>
        void ModifyReputation(int delta);

        /// <summary>
        /// Set reputation to a specific value.
        /// Automatically clamps to [-100, +100] range.
        /// </summary>
        /// <param name="value">New reputation value.</param>
        void SetReputation(int value);

        /// <summary>
        /// Calculate standing for a given reputation value.
        /// Useful for previewing what standing a certain reputation would give.
        /// </summary>
        /// <param name="reputationValue">Reputation to check.</param>
        /// <returns>The standing for that reputation level.</returns>
        FactionStanding CalculateStanding(int reputationValue);
    }
}
