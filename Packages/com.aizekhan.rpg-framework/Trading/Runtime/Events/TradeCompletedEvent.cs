using RPGFramework.Events;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Event raised when a trade is fully completed and finalized.
    /// This is the final event in a trade lifecycle.
    /// Published by <c>TradingManager.AcceptTrade</c> right after TradeAcceptedEvent (accepting finishes the trade at once).
    /// TotalValue is the gold moved: items carry no price in this module. Timestamp is 0: the manager keeps no clock.
    /// </summary>
    public struct TradeCompletedEvent : IEvent
    {
        /// <summary>Entity that completed the trade</summary>
        public int PlayerEntityId;

        /// <summary>ID of the trader</summary>
        public string TraderId;

        /// <summary>ID of the completed trade offer</summary>
        public string TradeOfferId;

        /// <summary>Total value of items exchanged</summary>
        public long TotalValue;

        /// <summary>New reputation level with trader</summary>
        public int NewReputation;

        /// <summary>Timestamp when trade was completed</summary>
        public float Timestamp;
    }
}
