using RPGFramework.Events;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Event raised when a player declines a trade offer.
    /// Published by <c>TradingManager.DeclineTrade</c> when a proposed offer is dropped, with a null Reason (DeclineTrade
    /// takes none), and by <c>TradingManager.Trade</c> when the trade it proposed fails, with the failure as Reason and Error.
    /// Timestamp is 0 (the manager keeps no clock).
    /// </summary>
    public struct TradeDeclinedEvent : IEvent
    {
        /// <summary>Entity that declined the trade</summary>
        public int PlayerEntityId;

        /// <summary>ID of the trader</summary>
        public string TraderId;

        /// <summary>ID of the declined trade offer</summary>
        public string TradeOfferId;

        /// <summary>Reason for declining (optional)</summary>
        public string Reason;

        /// <summary>Why, as a code (None when the player declined it themselves)</summary>
        public TradeError Error;

        /// <summary>Timestamp when trade was declined</summary>
        public float Timestamp;
    }
}
