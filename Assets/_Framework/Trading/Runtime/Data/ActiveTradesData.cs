using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Component data for entities that participate in trading.
    /// Tracks open trades and trader reputation.
    /// </summary>
    [System.Serializable]
    public struct ActiveTradesData
    {
        /// <summary>List of currently active trade IDs for this entity</summary>
        public List<string> ActiveTradeIds;

        /// <summary>Trader reputation levels by trader ID</summary>
        public Dictionary<string, int> TraderReputation;

        /// <summary>Gold available for trading</summary>
        public long AvailableGold;

        /// <summary>Total gold gained from trades</summary>
        public long TotalGoldGained;

        /// <summary>Total gold spent on trades</summary>
        public long TotalGoldSpent;

        /// <summary>Number of completed trades</summary>
        public int CompletedTradeCount;

        public ActiveTradesData(long initialGold = 0)
        {
            ActiveTradeIds = new List<string>();
            TraderReputation = new Dictionary<string, int>();
            AvailableGold = initialGold;
            TotalGoldGained = 0;
            TotalGoldSpent = 0;
            CompletedTradeCount = 0;
        }

        /// <summary>
        /// Get reputation with a specific trader.
        /// </summary>
        public int GetReputation(string traderId)
        {
            if (TraderReputation == null)
                return 0;
            return TraderReputation.TryGetValue(traderId, out var rep) ? rep : 0;
        }

        /// <summary>
        /// Add reputation with a trader.
        /// </summary>
        public void AddReputation(string traderId, int amount)
        {
            if (TraderReputation == null)
                TraderReputation = new Dictionary<string, int>();
            if (!TraderReputation.ContainsKey(traderId))
                TraderReputation[traderId] = 0;
            TraderReputation[traderId] += amount;
        }

        /// <summary>
        /// Add gold (from sales or quest rewards).
        /// </summary>
        public void AddGold(long amount)
        {
            AvailableGold += amount;
            TotalGoldGained += amount;
        }

        /// <summary>
        /// Spend gold (for purchases).
        /// </summary>
        public bool SpendGold(long amount)
        {
            if (AvailableGold >= amount)
            {
                AvailableGold -= amount;
                TotalGoldSpent += amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Record a completed trade.
        /// </summary>
        public void RecordCompletedTrade(string tradeId)
        {
            CompletedTradeCount++;
            if (ActiveTradeIds == null)
                ActiveTradeIds = new List<string>();
            if (ActiveTradeIds.Contains(tradeId))
                ActiveTradeIds.Remove(tradeId);
        }
    }
}
