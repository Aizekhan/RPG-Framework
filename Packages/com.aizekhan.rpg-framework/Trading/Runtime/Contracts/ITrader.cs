using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Represents a trader entity in the world.
    /// Manages inventory, pricing, and reputation requirements.
    /// </summary>
    public interface ITrader
    {
        /// <summary>Unique identifier for this trader</summary>
        string TraderId { get; }

        /// <summary>Display name of the trader</summary>
        string Name { get; }

        /// <summary>Type of trader (Merchant, Blacksmith, etc.)</summary>
        TraderType TraderType { get; }

        /// <summary>All available trade offers from this trader</summary>
        IReadOnlyList<ITradeOffer> AvailableOffers { get; }

        /// <summary>Price modifier for items (1.0 = standard price)</summary>
        float PriceModifier { get; }

        /// <summary>Minimum reputation required to trade with this trader</summary>
        int MinimumReputation { get; }

        /// <summary>
        /// Get a specific trade offer by ID.
        /// </summary>
        ITradeOffer? GetOffer(string offerId);

        /// <summary>
        /// Get all offers that meet player conditions.
        /// </summary>
        IReadOnlyList<ITradeOffer> GetAvailableOffers(int playerEntityId);

        /// <summary>
        /// Check if player meets minimum reputation requirement.
        /// </summary>
        bool MeetsReputationRequirement(int playerReputation);

        /// <summary>
        /// Get reputation gain for completing a trade with this trader.
        /// </summary>
        int GetReputationGain();
    }
}
