namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Loyalty/affection level of a companion toward its owner.
    /// Affects companion behavior, obedience, and special bonuses.
    /// </summary>
    public enum LoyaltyLevel
    {
        /// <summary>
        /// Companion actively dislikes owner, may refuse commands or flee.
        /// Loyalty: -100 to -51
        /// </summary>
        Hostile = 0,

        /// <summary>
        /// Companion is cautious and distrustful.
        /// Loyalty: -50 to -1
        /// </summary>
        Wary = 1,

        /// <summary>
        /// Companion has no strong feelings, follows basic commands.
        /// Loyalty: 0 to 24
        /// </summary>
        Neutral = 2,

        /// <summary>
        /// Companion likes the owner and is cooperative.
        /// Loyalty: 25 to 49
        /// </summary>
        Friendly = 3,

        /// <summary>
        /// Companion is loyal and will fight fiercely for the owner.
        /// Loyalty: 50 to 74
        /// </summary>
        Loyal = 4,

        /// <summary>
        /// Companion is completely devoted, maximum effectiveness and bonuses.
        /// Loyalty: 75 to 100
        /// </summary>
        Devoted = 5
    }
}
