namespace RPGFramework.Trading
{
    /// <summary>
    /// Type of condition that must be met to complete a trade.
    /// </summary>
    public enum ConditionType
    {
        /// <summary>
        /// Player must have minimum reputation with this trader.
        /// ParameterId is trader ID, Value is minimum reputation level.
        /// </summary>
        ReputationLevel = 0,

        /// <summary>
        /// Player must have completed a specific quest.
        /// ParameterId is quest ID, Value is ignored.
        /// </summary>
        QuestComplete = 1,

        /// <summary>
        /// Player must have standing with a faction.
        /// ParameterId is faction ID, Value is minimum standing.
        /// </summary>
        FactionStanding = 2,

        /// <summary>
        /// Player must be a minimum level.
        /// ParameterId is ignored, Value is minimum player level.
        /// </summary>
        PlayerLevel = 3
    }
}
