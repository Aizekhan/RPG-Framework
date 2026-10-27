using RPGFramework.Events;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Event raised when a player accepts a trade offer.
    /// Published by <c>TradingManager.AcceptTrade</c> once the trade has been applied, right before TradeCompletedEvent.
    /// Timestamp is 0: the manager keeps no clock (time is the game layer's job).
    /// </summary>
    public struct TradeAcceptedEvent : IEvent
    {
        /// <summary>Entity that accepted the trade</summary>
        public int PlayerEntityId;

        /// <summary>ID of the trader</summary>
        public string TraderId;

        /// <summary>ID of the accepted trade offer</summary>
        public string TradeOfferId;

        /// <summary>Number of items player received</summary>
        public int ItemsReceivedCount;

        /// <summary>Number of items player gave</summary>
        public int ItemsGivenCount;

        /// <summary>Gold change for player (positive = gain)</summary>
        public long GoldChange;

        /// <summary>Reputation change with trader</summary>
        public int ReputationChange;

        /// <summary>Timestamp when trade was accepted</summary>
        public float Timestamp;
    }
}
