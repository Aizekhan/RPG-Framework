using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Default implementation of ITradeOffer.
    /// </summary>
    public class TradeOffer : ITradeOffer
    {
        private TradeOfferData _data; // Not readonly - struct needs to be mutable
        private readonly List<TradeConditionData> _conditions;

        public string OfferId => _data.OfferId;
        public string TraderId => _data.TraderId;
        public IReadOnlyList<TradeItem> OfferedItems => _data.OfferedItems.AsReadOnly();
        public IReadOnlyList<TradeItem> RequestedItems => _data.RequestedItems.AsReadOnly();
        public long GoldAmount => _data.GoldAmount;
        public IReadOnlyList<TradeConditionData> Conditions => _data.Conditions.AsReadOnly();
        public bool IsAvailable => _data.IsAvailable;

        public TradeOffer(TradeOfferData data)
        {
            _data = data;
            _conditions = new List<TradeConditionData>(data.Conditions);
        }

        public bool CanBeUsedAgain()
        {
            return IsAvailable && (_data.MaxUses == 0 || _data.CurrentUses < _data.MaxUses);
        }

        public void IncrementUse()
        {
            _data.CurrentUses++;
        }

        /// <summary>
        /// Create a trade offer from raw data.
        /// </summary>
        public static TradeOffer Create(string offerId, string traderId)
        {
            var data = new TradeOfferData(offerId, traderId, 0);
            return new TradeOffer(data);
        }

        /// <summary>
        /// Add an offered item to this trade.
        /// </summary>
        public void AddOfferedItem(TradeItem item)
        {
            _data.OfferedItems.Add(item);
        }

        /// <summary>
        /// Add a requested item to this trade.
        /// </summary>
        public void AddRequestedItem(TradeItem item)
        {
            _data.RequestedItems.Add(item);
        }

        /// <summary>
        /// Add a condition to this trade.
        /// </summary>
        public void AddCondition(TradeConditionData condition)
        {
            _data.Conditions.Add(condition);
        }

        /// <summary>
        /// Set the gold amount for this trade.
        /// </summary>
        public void SetGoldAmount(long goldAmount)
        {
            _data.GoldAmount = goldAmount;
        }

        /// <summary>
        /// Set maximum uses for this offer (0 = unlimited).
        /// </summary>
        public void SetMaxUses(int maxUses)
        {
            _data.MaxUses = maxUses;
        }
    }
}
