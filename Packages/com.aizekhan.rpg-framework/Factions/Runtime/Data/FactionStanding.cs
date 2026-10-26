namespace RPGFramework.Factions
{
    /// <summary>
    /// Represents the standing/relationship level between an entity and a faction.
    /// Based on reputation value thresholds.
    /// </summary>
    public enum FactionStanding
    {
        /// <summary>
        /// Extremely negative relationship (reputation -100 to -75).
        /// Faction members attack on sight, no trading, quests unavailable.
        /// </summary>
        Hated = 0,

        /// <summary>
        /// Very negative relationship (reputation -74 to -50).
        /// Faction is hostile, limited interactions possible.
        /// </summary>
        Hostile = 1,

        /// <summary>
        /// Negative relationship (reputation -49 to -25).
        /// Faction distrusts you, poor prices, basic quests only.
        /// </summary>
        Unfriendly = 2,

        /// <summary>
        /// No established relationship (reputation -24 to +24).
        /// Standard interactions, normal prices.
        /// </summary>
        Neutral = 3,

        /// <summary>
        /// Positive relationship (reputation +25 to +49).
        /// Faction trusts you, better prices, more quests available.
        /// </summary>
        Friendly = 4,

        /// <summary>
        /// Very positive relationship (reputation +50 to +74).
        /// Faction considers you an ally, special discounts, unique quests.
        /// </summary>
        Honored = 5,

        /// <summary>
        /// Very positive relationship (reputation +50 to +74).
        /// Faction considers you an ally, special discounts, unique quests.
        /// </summary>
        Revered = 6,

        /// <summary>
        /// Maximum positive relationship (reputation +75 to +100).
        /// Faction's highest rank, best rewards, exclusive content.
        /// </summary>
        Exalted = 7
    }
}
