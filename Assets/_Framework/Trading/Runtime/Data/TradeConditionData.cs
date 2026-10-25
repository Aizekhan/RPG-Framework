namespace RPGFramework.Trading
{
    /// <summary>
    /// Serializable condition data for a trade.
    /// Represents a single requirement that must be met.
    /// </summary>
    [System.Serializable]
    public struct TradeConditionData
    {
        /// <summary>Type of condition to check</summary>
        public ConditionType Type;

        /// <summary>ID parameter (trader ID, quest ID, faction ID, or ignored)</summary>
        public string ParameterId;

        /// <summary>Value parameter (reputation level, player level, or ignored)</summary>
        public int Value;

        public TradeConditionData(ConditionType type, string parameterId, int value)
        {
            Type = type;
            ParameterId = parameterId;
            Value = value;
        }

        /// <summary>
        /// Creates a reputation requirement condition.
        /// </summary>
        public static TradeConditionData RequireReputation(string traderId, int minReputation)
        {
            return new TradeConditionData(ConditionType.ReputationLevel, traderId, minReputation);
        }

        /// <summary>
        /// Creates a quest completion requirement.
        /// </summary>
        public static TradeConditionData RequireQuestComplete(string questId)
        {
            return new TradeConditionData(ConditionType.QuestComplete, questId, 0);
        }

        /// <summary>
        /// Creates a faction standing requirement.
        /// </summary>
        public static TradeConditionData RequireFactionStanding(string factionId, int minStanding)
        {
            return new TradeConditionData(ConditionType.FactionStanding, factionId, minStanding);
        }

        /// <summary>
        /// Creates a player level requirement.
        /// </summary>
        public static TradeConditionData RequirePlayerLevel(int minLevel)
        {
            return new TradeConditionData(ConditionType.PlayerLevel, "", minLevel);
        }
    }
}
