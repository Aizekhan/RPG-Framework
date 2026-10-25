using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// ECS-based manager for trading operations.
    /// Handles trade validation, execution, and state management.
    /// </summary>
    public interface ITradingManager
    {
        /// <summary>
        /// Propose a trade to a player.
        /// Validates all conditions and constraints.
        /// </summary>
        /// <param name="playerEntityId">Entity receiving the trade</param>
        /// <param name="offerId">ID of the offer to propose</param>
        /// <returns>True if proposal was accepted and can proceed</returns>
        bool ProposeTradeOffer(int playerEntityId, string offerId);

        /// <summary>
        /// Accept a proposed trade and execute it.
        /// Transfers items, gold, and records reputation changes.
        /// </summary>
        /// <param name="playerEntityId">Entity accepting the trade</param>
        /// <param name="offerId">ID of the offer to accept</param>
        /// <returns>Result of the trade execution</returns>
        TradeResultData AcceptTrade(int playerEntityId, string offerId);

        /// <summary>
        /// Decline a proposed trade.
        /// </summary>
        /// <param name="playerEntityId">Entity declining the trade</param>
        /// <param name="offerId">ID of the offer to decline</param>
        void DeclineTrade(int playerEntityId, string offerId);

        /// <summary>
        /// Get all available offers from a specific trader.
        /// </summary>
        /// <param name="traderId">ID of the trader</param>
        /// <param name="playerEntityId">Entity to check conditions against</param>
        /// <returns>List of available offers</returns>
        IReadOnlyList<ITradeOffer> GetTraderOffers(string traderId, int playerEntityId);

        /// <summary>
        /// Get player's current gold balance.
        /// </summary>
        long GetPlayerGold(int playerEntityId);

        /// <summary>
        /// Get player's reputation with a specific trader.
        /// </summary>
        int GetPlayerReputation(int playerEntityId, string traderId);

        /// <summary>
        /// Set player's reputation with a trader (for admin/testing).
        /// </summary>
        void SetPlayerReputation(int playerEntityId, string traderId, int reputation);

        /// <summary>
        /// Get number of times a player has completed a specific trade.
        /// </summary>
        int GetTradeUseCount(int playerEntityId, string offerId);

        /// <summary>
        /// Get all active trades for a player.
        /// </summary>
        IReadOnlyList<string> GetActiveTradeIds(int playerEntityId);
    }
}
