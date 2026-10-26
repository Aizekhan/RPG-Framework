namespace RPGFramework.Trading
{
    /// <summary>
    /// Event raised when a player declines a trade offer.
    /// </summary>
    public struct TradeDeclinedEvent
    {
        /// <summary>Entity that declined the trade</summary>
        public int PlayerEntityId;

        /// <summary>ID of the trader</summary>
        public string TraderId;

        /// <summary>ID of the declined trade offer</summary>
        public string TradeOfferId;

        /// <summary>Reason for declining (optional)</summary>
        public string Reason;

        /// <summary>Timestamp when trade was declined</summary>
        public float Timestamp;
    }
}
