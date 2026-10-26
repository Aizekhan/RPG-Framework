using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Represents a single trade offer from a trader.
    /// Defines items exchanged, gold, and conditions.
    /// </summary>
    public interface ITradeOffer
    {
        /// <summary>Unique identifier for this offer</summary>
        string OfferId { get; }

        /// <summary>ID of the trader making this offer</summary>
        string TraderId { get; }

        /// <summary>Items the trader is offering to the player</summary>
        IReadOnlyList<TradeItem> OfferedItems { get; }

        /// <summary>Items the trader is requesting from the player</summary>
        IReadOnlyList<TradeItem> RequestedItems { get; }

        /// <summary>Gold amount in the trade (positive = payment to player, negative = cost)</summary>
        long GoldAmount { get; }

        /// <summary>Conditions that must be met for this trade</summary>
        IReadOnlyList<TradeConditionData> Conditions { get; }

        /// <summary>Whether this offer is currently available</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Check if this offer can be used again (respects MaxUses and availability).
        /// </summary>
        bool CanBeUsedAgain();

        /// <summary>
        /// Record that this offer has been used once.
        /// </summary>
        void IncrementUse();
    }
}
