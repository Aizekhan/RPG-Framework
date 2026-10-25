using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Serializable data for a complete trade offer.
    /// Defines what items and gold are offered/requested and conditions.
    /// </summary>
    [System.Serializable]
    public struct TradeOfferData
    {
        /// <summary>Unique identifier for this offer</summary>
        public string OfferId;

        /// <summary>ID of trader making this offer</summary>
        public string TraderId;

        /// <summary>Items the trader is offering to the player</summary>
        public List<TradeItem> OfferedItems;

        /// <summary>Items the trader is requesting from the player</summary>
        public List<TradeItem> RequestedItems;

        /// <summary>Gold the trader will pay (negative = gold required)</summary>
        public long GoldAmount;

        /// <summary>Conditions that must be met for this trade</summary>
        public List<TradeConditionData> Conditions;

        /// <summary>Whether this offer is currently available</summary>
        public bool IsAvailable;

        /// <summary>Maximum number of times this offer can be used (0 = unlimited)</summary>
        public int MaxUses;

        /// <summary>Current uses of this offer</summary>
        public int CurrentUses;

        public TradeOfferData(string offerId, string traderId, long goldAmount = 0)
        {
            OfferId = offerId;
            TraderId = traderId;
            GoldAmount = goldAmount;
            OfferedItems = new List<TradeItem>();
            RequestedItems = new List<TradeItem>();
            Conditions = new List<TradeConditionData>();
            IsAvailable = true;
            MaxUses = 0;
            CurrentUses = 0;
        }

        /// <summary>
        /// Check if this offer can be used again.
        /// </summary>
        public bool CanBeUsedAgain()
        {
            return IsAvailable && (MaxUses == 0 || CurrentUses < MaxUses);
        }

        /// <summary>
        /// Increment use counter.
        /// </summary>
        public void IncrementUse()
        {
            CurrentUses++;
        }
    }
}
