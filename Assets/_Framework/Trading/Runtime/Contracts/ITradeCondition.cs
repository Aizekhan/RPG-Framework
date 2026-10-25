namespace RPGFramework.Trading
{
    /// <summary>
    /// Represents a single condition that must be met for a trade.
    /// Conditions are evaluated in context of player state.
    /// </summary>
    public interface ITradeCondition
    {
        /// <summary>Type of this condition</summary>
        ConditionType ConditionType { get; }

        /// <summary>ID parameter (trader, quest, faction, etc.)</summary>
        string ParameterId { get; }

        /// <summary>Value parameter (reputation level, player level, etc.)</summary>
        int Value { get; }

        /// <summary>
        /// Check if condition is met for player.
        /// </summary>
        /// <param name="playerEntityId">Entity ID of player</param>
        /// <returns>True if condition is satisfied</returns>
        bool IsMet(int playerEntityId);

        /// <summary>
        /// Get human-readable description of this condition.
        /// </summary>
        string GetDescription();
    }
}
