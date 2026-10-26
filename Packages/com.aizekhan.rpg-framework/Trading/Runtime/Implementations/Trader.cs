using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Default implementation of ITrader.
    /// Represents a trader NPC with offers.
    /// </summary>
    public class Trader : ITrader
    {
        private readonly string _traderId;
        private readonly string _name;
        private readonly TraderType _traderType;
        private readonly List<ITradeOffer> _offers;
        private readonly float _priceModifier;
        private readonly int _minimumReputation;
        private int _reputationGain;

        public string TraderId => _traderId;
        public string Name => _name;
        public TraderType TraderType => _traderType;
        public IReadOnlyList<ITradeOffer> AvailableOffers => _offers.AsReadOnly();
        public float PriceModifier => _priceModifier;
        public int MinimumReputation => _minimumReputation;

        public Trader(string traderId, string name, TraderType traderType, float priceModifier = 1.0f, int minimumReputation = 0)
        {
            _traderId = traderId;
            _name = name;
            _traderType = traderType;
            _offers = new List<ITradeOffer>();
            _priceModifier = priceModifier;
            _minimumReputation = minimumReputation;
            _reputationGain = 10;
        }

        public ITradeOffer? GetOffer(string offerId)
        {
            return _offers.FirstOrDefault(o => o.OfferId == offerId);
        }

        public IReadOnlyList<ITradeOffer> GetAvailableOffers(int playerEntityId)
        {
            return _offers.Where(o => o.IsAvailable && o.CanBeUsedAgain()).ToList().AsReadOnly();
        }

        public bool MeetsReputationRequirement(int playerReputation)
        {
            return playerReputation >= _minimumReputation;
        }

        public int GetReputationGain()
        {
            return _reputationGain;
        }

        /// <summary>
        /// Add a trade offer to this trader.
        /// </summary>
        public void AddOffer(ITradeOffer offer)
        {
            _offers.Add(offer);
        }

        /// <summary>
        /// Set reputation gain for trades with this trader.
        /// </summary>
        public void SetReputationGain(int amount)
        {
            _reputationGain = amount;
        }

        /// <summary>
        /// Create a basic trader.
        /// </summary>
        public static Trader Create(string traderId, string name, TraderType traderType)
        {
            return new Trader(traderId, name, traderType);
        }
    }
}
