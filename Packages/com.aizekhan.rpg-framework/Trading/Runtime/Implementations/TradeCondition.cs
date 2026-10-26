namespace RPGFramework.Trading
{
    /// <summary>
    /// Default implementation of ITradeCondition.
    /// Evaluates conditions based on data.
    /// </summary>
    public class TradeCondition : ITradeCondition
    {
        private readonly TradeConditionData _data;

        public ConditionType ConditionType => _data.Type;
        public string ParameterId => _data.ParameterId;
        public int Value => _data.Value;

        public TradeCondition(TradeConditionData data)
        {
            _data = data;
        }

        public bool IsMet(int playerEntityId)
        {
            // In the framework, conditions are checked by the trading manager
            // which has access to game state systems (reputation, quests, etc.)
            // This method provides the interface for evaluation.
            // Actual implementation is game-layer specific.
            return true;
        }

        public string GetDescription()
        {
            return _data.Type switch
            {
                ConditionType.ReputationLevel => $"Requires reputation {_data.Value} with trader {_data.ParameterId}",
                ConditionType.QuestComplete => $"Requires completion of quest {_data.ParameterId}",
                ConditionType.FactionStanding => $"Requires faction standing {_data.Value} with {_data.ParameterId}",
                ConditionType.PlayerLevel => $"Requires player level {_data.Value}",
                _ => "Unknown condition"
            };
        }
    }
}
