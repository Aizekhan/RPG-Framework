namespace RPGFramework.Trading
{
    /// <summary>
    /// Classification of trader by specialty and goods offered.
    /// </summary>
    public enum TraderType
    {
        /// <summary>
        /// General merchant selling common items and supplies.
        /// Buys most items at fair prices.
        /// </summary>
        Merchant = 0,

        /// <summary>
        /// Specializes in weapons and armor.
        /// High reputation requirement, better quality equipment.
        /// </summary>
        Blacksmith = 1,

        /// <summary>
        /// Sells potions, ingredients, and alchemical components.
        /// May have rare alchemical items.
        /// </summary>
        Alchemist = 2,

        /// <summary>
        /// Deals in stolen goods and contraband.
        /// No reputation requirement, illegal items.
        /// </summary>
        FenceDealer = 3,

        /// <summary>
        /// Provides special items as quest rewards.
        /// Quest-locked trades, unique items unavailable elsewhere.
        /// </summary>
        QuestGiver = 4
    }
}
