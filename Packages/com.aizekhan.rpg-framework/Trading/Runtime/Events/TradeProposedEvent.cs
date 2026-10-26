namespace RPGFramework.Trading
{
    /// <summary>
    /// Event raised when a trade offer is proposed to a player.
    /// </summary>
    public struct TradeProposedEvent
    {
        /// <summary>Entity receiving the trade proposal</summary>
        public int PlayerEntityId;

        /// <summary>ID of the trader making the offer</summary>
        public string TraderId;

        /// <summary>ID of the trade offer</summary>
        public string TradeOfferId;

        /// <summary>Number of items the trader is offering</summary>
        public int OfferedItemCount;

        /// <summary>Number of items the trader is requesting</summary>
        public int RequestedItemCount;

        /// <summary>Gold amount in the trade (positive = payment to player, negative = cost)</summary>
        public long GoldAmount;

        /// <summary>Timestamp when trade was proposed</summary>
        public float Timestamp;
    }
}
