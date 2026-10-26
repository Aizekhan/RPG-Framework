namespace RPGFramework.Factions
{
    /// <summary>
    /// Types of relationships between factions.
    /// Determines how factions interact and affect each other's reputation.
    /// </summary>
    public enum FactionRelationType
    {
        /// <summary>
        /// Factions have no special relationship.
        /// Reputation changes don't affect each other.
        /// </summary>
        Neutral = 0,

        /// <summary>
        /// Factions are friendly and cooperate.
        /// Gaining reputation with one slightly increases reputation with the other.
        /// Typical modifier: +0.25 to +0.5
        /// </summary>
        Allied = 1,

        /// <summary>
        /// Factions are at war or hostile.
        /// Gaining reputation with one decreases reputation with the other.
        /// Typical modifier: -0.5 to -1.0
        /// </summary>
        Enemy = 2,

        /// <summary>
        /// Factions compete for same goals/resources.
        /// Gaining reputation with one slightly decreases reputation with the other.
        /// Typical modifier: -0.25 to -0.5
        /// </summary>
        Rival = 3,

        /// <summary>
        /// Factions have trade agreements.
        /// Gaining reputation with one provides small bonuses with the other.
        /// Typical modifier: +0.15 to +0.3
        /// </summary>
        TradePartner = 4
    }
}
