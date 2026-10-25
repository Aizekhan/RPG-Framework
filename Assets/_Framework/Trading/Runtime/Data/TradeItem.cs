namespace RPGFramework.Trading
{
    /// <summary>
    /// Represents a single item in a trade (offered or requested).
    /// </summary>
    [System.Serializable]
    public struct TradeItem
    {
        /// <summary>Item ID from item registry</summary>
        public string ItemId;

        /// <summary>Quantity of this item</summary>
        public int Quantity;

        /// <summary>Minimum quality required (if requesting), or provided (if offering)</summary>
        public int MinQuality;

        public TradeItem(string itemId, int quantity, int minQuality = 0)
        {
            ItemId = itemId;
            Quantity = quantity;
            MinQuality = minQuality;
        }

        /// <summary>
        /// Creates a basic trade item with no quality requirement.
        /// </summary>
        public static TradeItem Create(string itemId, int quantity)
        {
            return new TradeItem(itemId, quantity, 0);
        }
    }
}
